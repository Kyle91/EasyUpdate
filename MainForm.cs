using System;
using System.Drawing;
using System.Windows.Forms;
using EasyUpdate.Models;

namespace EasyUpdate
{
    public partial class MainForm : Form
    {
        private UpdatePayload _payload;
        private UpdateEngine _engine;
        private Timer _startTimer;

        public MainForm(UpdatePayload payload)
        {
            _payload = payload;
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            // 设置更新说明
            if (!string.IsNullOrEmpty(_payload.UpdateContent))
            {
                lblUpdateContent.Text = _payload.UpdateContent.Replace("\\n", "\n");
            }

            // 初始化文件列表
            foreach (var item in _payload.List)
            {
                var listItem = new ListViewItem(item.GetDisplayName());
                listItem.SubItems.Add("等待中");
                listItem.SubItems.Add("-");
                listItem.Tag = item;
                listViewFiles.Items.Add(listItem);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 使用Timer延迟启动，避免UI阻塞
            _startTimer = new Timer();
            _startTimer.Interval = 100;
            _startTimer.Tick += (s, ev) =>
            {
                _startTimer.Stop();
                _startTimer.Dispose();
                StartUpdate();
            };
            _startTimer.Start();
        }

        private void StartUpdate()
        {
            _engine = new UpdateEngine(_payload);
            _engine.StatusChanged += OnStatusChanged;
            _engine.FileChanged += OnFileChanged;
            _engine.ProgressChanged += OnProgressChanged;
            _engine.FileStatusChanged += OnFileStatusChanged;
            _engine.UpdateCompleted += OnUpdateCompleted;
            _engine.ErrorOccurred += OnErrorOccurred;

            _engine.StartUpdateAsync();
        }

        private void OnStatusChanged(string status)
        {
            // 状态变化通过文件列表和进度条体现，不再单独显示
        }

        private void OnFileChanged(string fileName)
        {
            // 文件变化通过文件列表的状态列体现
        }

        private void OnProgressChanged(int percent, string detail)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, string>(OnProgressChanged), percent, detail);
                return;
            }
            progressBar.Value = Math.Min(100, Math.Max(0, percent));
            lblProgress.Text = detail;
        }

        private void OnFileStatusChanged(int index, string status, string size)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, string, string>(OnFileStatusChanged), index, status, size);
                return;
            }

            if (index >= 0 && index < listViewFiles.Items.Count)
            {
                var item = listViewFiles.Items[index];
                item.SubItems[1].Text = status;
                item.SubItems[2].Text = size;

                // 根据状态设置颜色
                if (status == "完成")
                {
                    item.ForeColor = Color.FromArgb(40, 167, 69);
                }
                else if (status == "下载中" || status == "处理中")
                {
                    item.ForeColor = Color.FromArgb(0, 123, 255);
                }
                else if (status == "失败" || status == "校验失败")
                {
                    item.ForeColor = Color.FromArgb(220, 53, 69);
                }

                // 确保当前项可见
                listViewFiles.EnsureVisible(index);
            }
        }

        private void OnUpdateCompleted(bool success)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(OnUpdateCompleted), success);
                return;
            }

            if (success)
            {
                lblProgress.Text = "更新完成";
                lblProgress.ForeColor = Color.FromArgb(40, 167, 69);
                progressBar.Value = 100;
            }

            // 延迟关闭让用户看到结果
            var timer = new Timer();
            timer.Interval = 1500;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                this.Close();
            };
            timer.Start();
        }

        private void OnErrorOccurred(string error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(OnErrorOccurred), error);
                return;
            }
            lblProgress.Text = "错误: " + error;
            lblProgress.ForeColor = Color.FromArgb(220, 53, 69);
        }
    }
}
