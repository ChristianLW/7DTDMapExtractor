using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace _7DTDMapExtractor {
	public class MainForm : Form {
		public MainForm() {
			SuspendLayout();
			// MapFileSelector
			MapFileSelector = new FileSelector("MapFileSelector", false);
			MapFileSelector.Panel.Location = new Point(12, 12);
			MapFileSelector.Panel.TabIndex = 0;
			MapFileSelector.Label.Text = "Map File";
			MapFileSelector.Dialog.Filter = "Player map files|*.map|All files|*.*";
			MapFileSelector.Dialog.InitialDirectory = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie");
			// ImageFileSelector
			OutputImageFileSelector = new FileSelector("OutputImageFileSelector", true);
			OutputImageFileSelector.Panel.Location = new Point(12, 41);
			OutputImageFileSelector.Panel.TabIndex = 1;
			OutputImageFileSelector.Label.Text = "Output File";
			OutputImageFileSelector.Dialog.Filter = "PNG files|*.png|All files|*.*";
			OutputImageFileSelector.Dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			// ExtractButton
			ExtractButton = new Button();
			ExtractButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			ExtractButton.Location = new Point(12, 76);
			ExtractButton.Size = new Size(376, 46);
			ExtractButton.Name = "ExtractButton";
			ExtractButton.TabIndex = 2;
			ExtractButton.Text = "Extract";
			ExtractButton.UseVisualStyleBackColor = true;
			ExtractButton.Click += (object sender, EventArgs e) => {
				MapExtractor.Extract(MapFileSelector.TextBox.Text, OutputImageFileSelector.TextBox.Text, ProgressBar, StatusLabel);
			};
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
			Controls.Add(MapFileSelector.Panel);
			Controls.Add(OutputImageFileSelector.Panel);
			Controls.Add(ExtractButton);
			Controls.Add(StatusLabel);
			Controls.Add(ProgressBar);
			ResumeLayout(false);
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
		class FileSelector {
			public readonly Panel Panel;
			public readonly Label Label;
			public readonly TextBox TextBox;
			public readonly Button Button;
			public readonly FileDialog Dialog;

			/// <param name="namePrefix">The prefix prepended to the name of all the controls.</param>
			/// <param name="isSave">Whether the FileDialog will be a SaveFileDialog or an OpenFileDialog.</param>
			public FileSelector(string namePrefix, bool isSave) {
				Panel = new Panel();
				Label = new Label();
				TextBox = new TextBox();
				Button = new Button();
				Dialog = isSave ? new SaveFileDialog() : new OpenFileDialog();
				Panel.SuspendLayout();
				// Panel
				Panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
				Panel.Size = new Size(376, 23);
				Panel.Name = namePrefix + "Panel";
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
					Dialog.FileName = TextBox.Text;
					if (Dialog.ShowDialog() == DialogResult.OK) {
						TextBox.Text = Dialog.FileName;
					}
				};
				// Finals
				Panel.Controls.Add(Label);
				Panel.Controls.Add(TextBox);
				Panel.Controls.Add(Button);
				Panel.ResumeLayout(false);
				Panel.PerformLayout();
			}
		}
	}
}
