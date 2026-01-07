using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyUpdate
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private CustomProgressBar progressBar;
        private Label lblProgress;
        private Label lblUpdateContent;
        private ListView listViewFiles;
        private Panel panelContent;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 窗体设置
            this.Text = "软件更新";
            this.Size = new Size(480, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.ForeColor = Color.FromArgb(33, 37, 41);
            this.Font = new Font("Microsoft YaHei UI", 9F);

            // 设置窗体图标（从嵌入资源加载）
            try
            {
                var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("EasyUpdate.icon.ico");
                if (stream != null)
                {
                    this.Icon = new System.Drawing.Icon(stream);
                }
            }
            catch { }

            // 内容面板（整个窗体）
            panelContent = new Panel();
            panelContent.Location = new Point(0, 0);
            panelContent.Size = new Size(480, 320);
            panelContent.BackColor = Color.FromArgb(248, 249, 250);

            // 更新说明（顶部）
            lblUpdateContent = new Label();
            lblUpdateContent.Location = new Point(24, 16);
            lblUpdateContent.Size = new Size(416, 50);
            lblUpdateContent.Text = "";
            lblUpdateContent.ForeColor = Color.FromArgb(73, 80, 87);
            lblUpdateContent.Font = new Font("Microsoft YaHei UI", 9F);
            panelContent.Controls.Add(lblUpdateContent);

            // 文件列表
            listViewFiles = new ListView();
            listViewFiles.Location = new Point(24, 70);
            listViewFiles.Size = new Size(416, 140);
            listViewFiles.View = View.Details;
            listViewFiles.FullRowSelect = true;
            listViewFiles.GridLines = false;
            listViewFiles.HeaderStyle = ColumnHeaderStyle.None;
            listViewFiles.BorderStyle = BorderStyle.None;
            listViewFiles.BackColor = Color.White;
            listViewFiles.ForeColor = Color.FromArgb(73, 80, 87);
            listViewFiles.Font = new Font("Microsoft YaHei UI", 9F);

            // 添加列
            listViewFiles.Columns.Add("文件名", 220);
            listViewFiles.Columns.Add("状态", 100);
            listViewFiles.Columns.Add("大小", 80);

            panelContent.Controls.Add(listViewFiles);

            // 自定义进度条
            progressBar = new CustomProgressBar();
            progressBar.Location = new Point(24, 230);
            progressBar.Size = new Size(416, 10);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            panelContent.Controls.Add(progressBar);

            // 进度文本（百分比 + 大小）
            lblProgress = new Label();
            lblProgress.Location = new Point(24, 246);
            lblProgress.Size = new Size(416, 18);
            lblProgress.Text = "";
            lblProgress.ForeColor = Color.FromArgb(108, 117, 125);
            lblProgress.Font = new Font("Microsoft YaHei UI", 9F);
            lblProgress.TextAlign = ContentAlignment.MiddleRight;
            panelContent.Controls.Add(lblProgress);

            // 添加控件
            this.Controls.Add(panelContent);

            this.ResumeLayout(false);
        }
    }

    // 自定义圆角进度条
    public class CustomProgressBar : Control
    {
        private int _minimum = 0;
        private int _maximum = 100;
        private int _value = 0;

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                _value = Math.Min(Math.Max(value, _minimum), _maximum);
                Invalidate();
            }
        }

        public CustomProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width, Height);
            var radius = Height / 2;

            // 背景
            using (var path = CreateRoundRect(rect, radius))
            using (var brush = new SolidBrush(Color.FromArgb(233, 236, 239)))
            {
                g.FillPath(brush, path);
            }

            // 进度
            if (_value > _minimum)
            {
                var progressWidth = (int)((Width - 1) * ((double)(_value - _minimum) / (_maximum - _minimum)));
                if (progressWidth > 0)
                {
                    var progressRect = new Rectangle(0, 0, progressWidth, Height);
                    using (var path = CreateRoundRect(progressRect, radius))
                    using (var brush = new LinearGradientBrush(progressRect, Color.FromArgb(0, 123, 255), Color.FromArgb(0, 86, 179), LinearGradientMode.Horizontal))
                    {
                        g.FillPath(brush, path);
                    }
                }
            }
        }

        private GraphicsPath CreateRoundRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius > 0)
            {
                path.AddArc(rect.X, rect.Y, radius * 2, rect.Height, 90, 180);
                path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, rect.Height, 270, 180);
                path.CloseFigure();
            }
            else
            {
                path.AddRectangle(rect);
            }
            return path;
        }
    }
}
