using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _7DTDMapExtractor {
	public partial class MockupForm : Form {
		public MockupForm() {
			InitializeComponent();
		}

		private void Button_Click(object sender, EventArgs e) {
			Dialog.FileName = TextBox.Text;
			if (Dialog.ShowDialog() == DialogResult.OK) {
				TextBox.Text = Dialog.FileName;
			}
		}
	}
}
