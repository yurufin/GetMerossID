
namespace GetMerossID
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        private void InitializeComponent()
        {
            this.label1       = new System.Windows.Forms.Label();
            this.labelStatus  = new System.Windows.Forms.Label();
            this.labelProgress = new System.Windows.Forms.Label();
            this.textBox1     = new System.Windows.Forms.TextBox();
            this.buttonSave   = new System.Windows.Forms.Button();
            this.buttonReset  = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // ──────────────────────────────
            // label1 ： タイトル
            // ──────────────────────────────
            this.label1.AutoSize  = true;
            this.label1.Font      = new System.Drawing.Font("メイリオ", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label1.Location  = new System.Drawing.Point(24, 18);
            this.label1.Name      = "label1";
            this.label1.TabIndex  = 0;
            this.label1.Text      = "★ Meross 電力計ID 自動取得";

            // ──────────────────────────────
            // labelStatus ： プロキシ情報（起動時に書き換え）
            // ──────────────────────────────
            this.labelStatus.AutoSize  = false;
            this.labelStatus.Font      = new System.Drawing.Font("メイリオ", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.labelStatus.ForeColor = System.Drawing.Color.DarkBlue;
            this.labelStatus.Location  = new System.Drawing.Point(24, 68);
            this.labelStatus.Name      = "labelStatus";
            this.labelStatus.Size      = new System.Drawing.Size(1419, 70);
            this.labelStatus.TabIndex  = 1;
            this.labelStatus.Text      = "プロキシを起動しています…";

            // ──────────────────────────────
            // labelProgress ： 取得進捗
            // ──────────────────────────────
            this.labelProgress.AutoSize  = true;
            this.labelProgress.Font      = new System.Drawing.Font("メイリオ", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.labelProgress.ForeColor = System.Drawing.Color.DarkGreen;
            this.labelProgress.Location  = new System.Drawing.Point(24, 148);
            this.labelProgress.Name      = "labelProgress";
            this.labelProgress.TabIndex  = 2;
            this.labelProgress.Text      = "取得済みトークン: 0 / 21";

            // ──────────────────────────────
            // buttonSave ： 手動保存
            // ──────────────────────────────
            this.buttonSave.Font      = new System.Drawing.Font("メイリオ", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.buttonSave.Location  = new System.Drawing.Point(1106, 18);
            this.buttonSave.Name      = "buttonSave";
            this.buttonSave.Size      = new System.Drawing.Size(160, 44);
            this.buttonSave.TabIndex  = 3;
            this.buttonSave.Text      = "CSV 保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click    += new System.EventHandler(this.buttonSave_Click);

            // ──────────────────────────────
            // buttonReset ： リセット
            // ──────────────────────────────
            this.buttonReset.Font      = new System.Drawing.Font("メイリオ", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.buttonReset.ForeColor = System.Drawing.Color.DarkRed;
            this.buttonReset.Location  = new System.Drawing.Point(1280, 18);
            this.buttonReset.Name      = "buttonReset";
            this.buttonReset.Size      = new System.Drawing.Size(160, 44);
            this.buttonReset.TabIndex  = 4;
            this.buttonReset.Text      = "リセット";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click    += new System.EventHandler(this.buttonReset_Click);

            // ──────────────────────────────
            // textBox1 ： 収集データ表示
            // ──────────────────────────────
            this.textBox1.Font        = new System.Drawing.Font("メイリオ", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.textBox1.Location    = new System.Drawing.Point(24, 190);
            this.textBox1.Multiline   = true;
            this.textBox1.Name        = "textBox1";
            this.textBox1.ReadOnly    = true;
            this.textBox1.ScrollBars  = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size        = new System.Drawing.Size(1419, 620);
            this.textBox1.TabIndex    = 5;
            this.textBox1.Text        = "";

            // ──────────────────────────────
            // Form1
            // ──────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 21F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(1467, 847);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelProgress);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonReset);
            this.Controls.Add(this.textBox1);
            this.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            this.Name   = "Form1";
            this.Text   = "Meross 電力計ID 自動取得";
            this.Load  += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label   label1;
        private System.Windows.Forms.Label   labelStatus;
        private System.Windows.Forms.Label   labelProgress;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button  buttonSave;
        private System.Windows.Forms.Button  buttonReset;
    }
}
