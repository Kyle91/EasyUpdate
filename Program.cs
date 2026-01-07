using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EasyUpdate
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // 读取update文件
                var updateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");

                if (!File.Exists(updateFilePath))
                {
                    MessageBox.Show("未找到更新配置文件", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 读取Base64内容
                var base64Content = File.ReadAllText(updateFilePath, Encoding.UTF8).Trim();

                // 立即删除文件
                try
                {
                    File.Delete(updateFilePath);
                }
                catch { }

                // Base64解码
                var jsonBytes = Convert.FromBase64String(base64Content);
                var jsonContent = Encoding.UTF8.GetString(jsonBytes);

                // 解析JSON
                var payload = JsonParser.ParseUpdatePayload(jsonContent);

                if (payload.List == null || payload.List.Count == 0)
                {
                    MessageBox.Show("没有需要更新的文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 启动更新窗口
                Application.Run(new MainForm(payload));
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动更新程序失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
