using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EasyUpdate.Models;

namespace EasyUpdate
{
    public class UpdateEngine
    {
        private readonly UpdatePayload _payload;
        private readonly string _baseDir;
        private readonly string _tempDir;
        private readonly List<PendingFile> _pendingFiles;
        private BackgroundWorker _worker;

        public event Action<string> StatusChanged;
        public event Action<string> FileChanged;
        public event Action<int, string> ProgressChanged;
        public event Action<int, string, string> FileStatusChanged;
        public event Action<bool> UpdateCompleted;
        public event Action<string> ErrorOccurred;

        private class PendingFile
        {
            public string TempPath { get; set; }
            public string TargetPath { get; set; }
        }

        public UpdateEngine(UpdatePayload payload)
        {
            _payload = payload;
            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _tempDir = Path.Combine(_baseDir, "update_temp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _pendingFiles = new List<PendingFile>();

            // 设置TLS支持
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch { }
        }

        private void RaiseStatusChanged(string status)
        {
            var handler = StatusChanged;
            if (handler != null) handler(status);
        }

        private void RaiseFileChanged(string fileName)
        {
            var handler = FileChanged;
            if (handler != null) handler(fileName);
        }

        private void RaiseProgressChanged(int percent, string detail)
        {
            var handler = ProgressChanged;
            if (handler != null) handler(percent, detail);
        }

        private void RaiseFileStatusChanged(int index, string status, string size)
        {
            var handler = FileStatusChanged;
            if (handler != null) handler(index, status, size);
        }

        private void RaiseUpdateCompleted(bool success)
        {
            var handler = UpdateCompleted;
            if (handler != null) handler(success);
        }

        private void RaiseErrorOccurred(string error)
        {
            var handler = ErrorOccurred;
            if (handler != null) handler(error);
        }

        public void StartUpdateAsync()
        {
            _worker = new BackgroundWorker();
            _worker.DoWork += Worker_DoWork;
            _worker.RunWorkerCompleted += Worker_Completed;
            _worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_tempDir);

                // 下载所有文件
                RaiseStatusChanged("正在下载更新文件...");
                DownloadAllFiles();

                // 等待主程序退出
                if (!string.IsNullOrEmpty(_payload.MainProcess))
                {
                    RaiseStatusChanged("等待程序退出...");
                    WaitForProcessExit(_payload.MainProcess);
                }

                // 替换文件
                RaiseStatusChanged("正在更新文件...");
                ReplaceFiles();

                // 清理临时目录
                CleanupTempDir();

                // 启动主程序
                if (!string.IsNullOrEmpty(_payload.MainExe))
                {
                    var exePath = Path.Combine(_baseDir, _payload.MainExe);
                    if (File.Exists(exePath))
                    {
                        Process.Start(exePath);
                    }
                }

                e.Result = true;
            }
            catch (Exception ex)
            {
                RaiseErrorOccurred(ex.Message);
                e.Result = false;
            }
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            var success = e.Result != null && (bool)e.Result;
            RaiseUpdateCompleted(success);
        }

        private void DownloadAllFiles()
        {
            var totalItems = _payload.List.Count;
            for (var i = 0; i < totalItems; i++)
            {
                var item = _payload.List[i];
                var displayName = item.GetDisplayName();
                RaiseFileChanged(displayName);
                RaiseFileStatusChanged(i, "下载中", "-");

                var tempFilePath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + GetExtension(item.Url));

                // 下载文件（带重试）
                long fileSize = 0;
                var downloaded = DownloadWithRetry(item.Url, tempFilePath, i, totalItems, out fileSize);
                if (!downloaded)
                {
                    RaiseFileStatusChanged(i, "失败", "-");
                    continue;
                }

                // MD5校验
                if (!string.IsNullOrEmpty(item.Md5))
                {
                    RaiseFileStatusChanged(i, "校验中", FormatBytes(fileSize));
                    var fileMd5 = ComputeMd5(tempFilePath);
                    if (!string.Equals(fileMd5, item.Md5, StringComparison.OrdinalIgnoreCase))
                    {
                        RaiseFileStatusChanged(i, "校验失败", FormatBytes(fileSize));
                        RaiseErrorOccurred(string.Format("文件 {0} MD5校验失败", displayName));
                        continue;
                    }
                }

                // 处理文件
                RaiseFileStatusChanged(i, "处理中", FormatBytes(fileSize));
                if (item.IsZip)
                {
                    ProcessZipFile(tempFilePath, item);
                }
                else
                {
                    ProcessSingleFile(tempFilePath, item);
                }

                RaiseFileStatusChanged(i, "完成", FormatBytes(fileSize));
            }
        }

        private bool DownloadWithRetry(string url, string savePath, int itemIndex, int totalItems, out long fileSize)
        {
            var retryDelays = new[] { 1000, 2000, 3000 };
            fileSize = 0;

            for (var retry = 0; retry <= 3; retry++)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;

                        var completed = false;
                        Exception downloadError = null;
                        long totalBytes = 0;

                        client.DownloadProgressChanged += (s, e) =>
                        {
                            totalBytes = e.TotalBytesToReceive;
                            var percent = (int)((itemIndex * 100.0 / totalItems) + (e.ProgressPercentage / (double)totalItems));
                            var detail = FormatBytes(e.BytesReceived);
                            if (e.TotalBytesToReceive > 0)
                            {
                                detail += " / " + FormatBytes(e.TotalBytesToReceive);
                            }
                            RaiseProgressChanged(percent, string.Format("{0}% - {1}", percent, detail));
                            RaiseFileStatusChanged(itemIndex, "下载中", detail);
                        };

                        client.DownloadFileCompleted += (s, e) =>
                        {
                            downloadError = e.Error;
                            completed = true;
                        };

                        client.DownloadFileAsync(new Uri(url), savePath);

                        // 等待下载完成
                        while (!completed)
                        {
                            Thread.Sleep(100);
                        }

                        if (downloadError != null)
                        {
                            throw downloadError;
                        }

                        // 获取文件大小
                        if (File.Exists(savePath))
                        {
                            fileSize = new FileInfo(savePath).Length;
                        }

                        return true;
                    }
                }
                catch
                {
                    if (retry < 3)
                    {
                        Thread.Sleep(retryDelays[retry]);
                    }
                }
            }

            RaiseErrorOccurred(string.Format("下载失败: {0}", url));
            return false;
        }

        private void ProcessZipFile(string zipPath, UpdateListItem item)
        {
            var extractDir = Path.Combine(_tempDir, "extract_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(extractDir);

            try
            {
                // 使用Shell32解压（兼容.NET 4.0）
                ExtractZipUsingShell(zipPath, extractDir);

                // 遍历解压的文件
                var files = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    // 如果指定了extract_name，只提取匹配的文件
                    if (!string.IsNullOrEmpty(item.ExtractName))
                    {
                        if (!string.Equals(fileName, item.ExtractName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    // 计算目标路径
                    var targetDir = _baseDir;
                    if (!string.IsNullOrEmpty(item.SavePath))
                    {
                        targetDir = Path.Combine(_baseDir, item.SavePath);
                    }

                    var targetPath = Path.Combine(targetDir, fileName);
                    _pendingFiles.Add(new PendingFile
                    {
                        TempPath = file,
                        TargetPath = targetPath
                    });
                }
            }
            catch (Exception ex)
            {
                RaiseErrorOccurred(string.Format("解压失败: {0}", ex.Message));
            }
        }

        private void ExtractZipUsingShell(string zipPath, string extractDir)
        {
            // 使用Shell32.dll解压，兼容Windows 7+，支持中文文件名
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(shellType);

            var zipFolder = shell.NameSpace(zipPath);
            var destFolder = shell.NameSpace(extractDir);

            if (zipFolder == null)
            {
                throw new Exception("无法打开压缩文件");
            }

            if (destFolder == null)
            {
                throw new Exception("无法访问目标目录");
            }

            // 4 = 不显示进度对话框
            // 16 = 对所有选择"是"
            destFolder.CopyHere(zipFolder.Items(), 4 | 16);

            // 等待解压完成
            Thread.Sleep(500);
            while (IsFileLocked(extractDir))
            {
                Thread.Sleep(200);
            }
        }

        private bool IsFileLocked(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        using (var stream = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            stream.Close();
                        }
                    }
                    catch (IOException)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void ProcessSingleFile(string tempPath, UpdateListItem item)
        {
            var targetDir = _baseDir;
            if (!string.IsNullOrEmpty(item.SavePath))
            {
                targetDir = Path.Combine(_baseDir, item.SavePath);
            }

            var fileName = item.ExtractName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = item.Name;
            }
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = Path.GetFileName(new Uri(item.Url).LocalPath);
            }

            var targetPath = Path.Combine(targetDir, fileName);
            _pendingFiles.Add(new PendingFile
            {
                TempPath = tempPath,
                TargetPath = targetPath
            });
        }

        private void WaitForProcessExit(string processName)
        {
            // 移除.exe后缀
            processName = processName.Replace(".exe", "").Replace(".EXE", "");

            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(60);

            while (DateTime.Now - startTime < timeout)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    return;
                }

                foreach (var p in processes)
                {
                    p.Dispose();
                }

                Thread.Sleep(500);
            }
        }

        private void ReplaceFiles()
        {
            var total = _pendingFiles.Count;
            for (var i = 0; i < total; i++)
            {
                var file = _pendingFiles[i];
                RaiseFileChanged(Path.GetFileName(file.TargetPath));
                RaiseProgressChanged((i + 1) * 100 / total, string.Format("正在替换 ({0}/{1})", i + 1, total));

                // 确保目标目录存在
                var dir = Path.GetDirectoryName(file.TargetPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 带重试的文件替换
                var success = false;
                for (var retry = 0; retry < 10 && !success; retry++)
                {
                    try
                    {
                        if (File.Exists(file.TargetPath))
                        {
                            File.Delete(file.TargetPath);
                        }
                        File.Copy(file.TempPath, file.TargetPath, true);
                        success = true;
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }

                if (!success)
                {
                    RaiseErrorOccurred(string.Format("无法替换文件: {0}", Path.GetFileName(file.TargetPath)));
                }
            }
        }

        private void CleanupTempDir()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        private string ComputeMd5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private string GetExtension(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.LocalPath;
                var ext = Path.GetExtension(path);
                return string.IsNullOrEmpty(ext) ? ".tmp" : ext;
            }
            catch
            {
                return ".tmp";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            var order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return string.Format("{0:0.##} {1}", size, sizes[order]);
        }
    }
}
