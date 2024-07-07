using System;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace _7DTDMapExtractor {
	public class MainForm : Form {
		public MainForm() {
			SuspendLayout();
			// MapFileSelector
			MapFileSelector = new FileSelector("MapFileSelector", false);
			MapFileSelector.Location = new Point(12, 12);
			MapFileSelector.TabIndex = 0;
			MapFileSelector.LabelText = "Map File";
			MapFileSelector.Filter = "Player map files|*.map;*.7rm|All files|*.*";
			MapFileSelector.InitialDirectory = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie");
			// ImageFileSelector
			OutputImageFileSelector = new FileSelector("OutputImageFileSelector", true);
			OutputImageFileSelector.Location = new Point(12, 41);
			OutputImageFileSelector.TabIndex = 1;
			OutputImageFileSelector.LabelText = "Output File";
			OutputImageFileSelector.Filter = "PNG files|*.png|All files|*.*";
			OutputImageFileSelector.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			// ExtractButton
			ExtractButton = new Button();
			ExtractButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			ExtractButton.Location = new Point(12, 76);
			ExtractButton.Size = new Size(376, 46);
			ExtractButton.Name = "ExtractButton";
			ExtractButton.TabIndex = 2;
			ExtractButton.Text = "Extract";
			ExtractButton.UseVisualStyleBackColor = true;
			ExtractButton.Click += (object sender, EventArgs e) => Extract();
			// StatusLabel
			StatusLabel = new Label();
			StatusLabel.AutoSize = true;
			StatusLabel.Location = new Point(12, 134);
			StatusLabel.Name = "StatusLabel";
			StatusLabel.TabIndex = 3;
			StatusLabel.Text = "Ready!";
			StatusLabel.ForeColor = Color.DarkGreen;
			// ProgressBar
			ProgressBar = new ProgressBar();
			ProgressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			ProgressBar.Location = new Point(12, 161);
			ProgressBar.Size = new Size(376, 23);
			ProgressBar.Name = "ProgressBar";
			ProgressBar.TabIndex = 4;
			ProgressBar.Style = ProgressBarStyle.Continuous;
			// MainForm
			AutoScaleMode = AutoScaleMode.None;
			ClientSize = new Size(400, 196);
			MinimumSize = Size;
			Name = "MainForm";
			Text = "7 Days to Die Map Extractor";
			Controls.Add(MapFileSelector);
			Controls.Add(OutputImageFileSelector);
			Controls.Add(ExtractButton);
			Controls.Add(StatusLabel);
			Controls.Add(ProgressBar);
			ResumeLayout(false);
		}

		public async void Extract() {
			ExtractButton.Enabled = false;
			StatusLabel.ForeColor = Color.DarkGreen;
			try {
				await Task.Run(() => MapExtractor.Extract(
					MapFileSelector.FileName,
					OutputImageFileSelector.FileName,
					progress => ProgressBar.Invoke(() => {
						ProgressBar.Value = progress;
					}), progressMax => ProgressBar.Invoke(() => {
						ProgressBar.Refresh();
						ProgressBar.Value = 0;
						ProgressBar.Maximum = progressMax;
					}), status => StatusLabel.Invoke(() => {
						StatusLabel.Text = status;
						StatusLabel.Refresh();
					})
				));
			} catch (Exception e) {
				StatusLabel.ForeColor = Color.DarkRed;
				StatusLabel.Text = e switch {
					ArgumentException => "You have to specify a file",
					DirectoryNotFoundException => "Directory doesn't exist",
					FileNotFoundException => "File doesn't exist",
					FileFormatException => e.Message,
					NotSupportedException => e.Message,
					_ => "Something went wrong"
				};
			}
			ExtractButton.Enabled = true;
		}

		private readonly FileSelector MapFileSelector;
		private readonly FileSelector OutputImageFileSelector;
		private readonly Button ExtractButton;
		private readonly Label StatusLabel;
		private readonly ProgressBar ProgressBar;

		/// <summary>
		///		A file selector "widget" containing a label, a text box and a button to open a file dialog.
		///		Everything is contained within a panel which stretches horizontally.
		///		Based on https://www.techcoil.com/blog/implement-a-file-chooser-in-windows-form/.
		/// </summary>
		class FileSelector : Panel {
			public readonly Label Label;
			public readonly TextBox TextBox;
			public readonly Button Button;
			public readonly FileDialog Dialog;

			public string LabelText { get => Label.Text; set => Label.Text = value; }
			public string Filter { get => Dialog.Filter; set => Dialog.Filter = value; }
			public string InitialDirectory { get; set; }
			public string FileName => TextBox.Text;

			/// <param name="namePrefix">The prefix prepended to the name of all the controls.</param>
			/// <param name="isSave">Whether the FileDialog will be a SaveFileDialog or an OpenFileDialog.</param>
			public FileSelector(string namePrefix, bool isSave) {
				Label = new Label();
				TextBox = new TextBox();
				Button = new Button();
				Dialog = isSave ? new SaveFileDialog() : new OpenFileDialog();
				SuspendLayout();
				// Panel
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
				Size = new Size(376, 23);
				Name = namePrefix + "Panel";
				// Label
				Label.AutoSize = true;
				Label.Location = new Point(0, 4);
				Label.Name = namePrefix + "Label";
				Label.TabIndex = 0;
				// TextBox
				TextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
				TextBox.Location = new Point(72, 0);
				TextBox.Size = new Size(240, 23);
				TextBox.Name = namePrefix + "TextBox";
				TextBox.TabIndex = 1;
				// Button
				Button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
				Button.Location = new Point(318, 0);
				Button.Size = new Size(58, 23);
				Button.Name = namePrefix + "Button";
				Button.TabIndex = 2;
				Button.Text = "Browse";
				Button.UseVisualStyleBackColor = true;
				Button.Click += (object sender, EventArgs e) => {
					string dir = Path.GetDirectoryName(TextBox.Text);
					Dialog.FileName = Path.GetFileName(TextBox.Text);
					Dialog.InitialDirectory = string.IsNullOrEmpty(dir) ? InitialDirectory : dir;
					if (Dialog.ShowDialog() == DialogResult.OK) {
						TextBox.Text = Dialog.FileName;
					}
				};
				// Finals
				Controls.Add(Label);
				Controls.Add(TextBox);
				Controls.Add(Button);
				ResumeLayout(false);
				PerformLayout();
			}
		}
	}
}
