using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace _7DTDMapExtractor {
	public static class MapExtractor {
		private const string MAGIC = "map\0";
		// Just set to the size of Navezgane for now, but should be easily changeable in the future
		private const int MAP_SIZE = 6144;
		private const int MAX_CHUNK = MAP_SIZE / 16 / 2;

		public static void Extract(string mapFile, string outputFile, ProgressBar progressBar, Label statusLabel) {
			try {
				using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				using BinaryReader reader = new BinaryReader(inputStream);
				progressBar.Value = 0;
				statusLabel.ForeColor = Color.DarkGreen;
				/*  ------  */
				/*  Header  */
				/*  ------  */
				statusLabel.Text = "Reading header";
				statusLabel.Refresh();
				if (new string(reader.ReadChars(4)) != MAGIC) {
					throw new FileFormatException("Invalid magic bytes");
				}
				byte version = reader.ReadByte();
				if (version != 3) {
					throw new NotSupportedException($"Unsupported file format version ({version})");
				}
				// Unused 3 bytes
				inputStream.Seek(3, SeekOrigin.Current);
				int maxChunks = reader.ReadInt32();
				int numChunks = reader.ReadInt32();
				progressBar.Maximum = numChunks - 1;
				/*  -----------------  */
				/*  Chunk Coordinates  */
				/*  -----------------  */
				statusLabel.Text = "Reading chunk coordinates";
				statusLabel.Refresh();
				uint[] chunkCoordinates = new uint[numChunks];
				for (int i = 0; i < numChunks; i++) {
					chunkCoordinates[i] = reader.ReadUInt32();
					progressBar.Value = i;
				}
				// Unused space
				inputStream.Seek(0x10 + maxChunks * 4, SeekOrigin.Begin);
				/*  ---------------------  */
				/*  Image Data Allocation  */
				/*  ---------------------  */
				statusLabel.Text = "Allocating and initialising image data";
				statusLabel.Refresh();
				progressBar.Value = 0;
				progressBar.Maximum = MAP_SIZE * MAP_SIZE;
				// Using a small zero-filled buffer here to *significantly*
				// speed up the zero-filling of the image data buffer
				// Writing the bytes individually directly in C# is really inefficient
				byte[] zeroBuffer = new byte[4096];
				IntPtr imageData = Marshal.AllocHGlobal(MAP_SIZE * MAP_SIZE * 2);
				for (int i = 0; i < (MAP_SIZE * MAP_SIZE * 2) >> 12; i++) {
					Marshal.Copy(zeroBuffer, 0, imageData + (i << 12), 4096);
					progressBar.Value = i;
				}
				/*  -----------  */
				/*  Colour Data  */
				/*  -----------  */
				statusLabel.Text = "Reading colour data and painting image";
				statusLabel.Refresh();
				progressBar.Value = 0;
				progressBar.Maximum = numChunks - 1;
				for (int i = 0; i < numChunks; i++) {
					int cx = (short)(chunkCoordinates[i] & 0xFFFFU);
					int cy = (short)(chunkCoordinates[i] >> 16);
					if (cx >= -MAX_CHUNK && cx < MAX_CHUNK && cy >= -MAX_CHUNK && cy < MAX_CHUNK) {
						for (int y = 0; y < 16; y++) {
							for (int x = 0; x < 16; x++) {
								ushort colour = reader.ReadUInt16();
								int offset = ((x + cx * 16 + MAP_SIZE / 2) + (MAP_SIZE - 1 - (y + cy * 16 + MAP_SIZE / 2)) * MAP_SIZE) * 2;
								Marshal.WriteInt16(imageData, offset, (short)(colour | 0x8000));
							}
						}
					} else {
						inputStream.Seek(512, SeekOrigin.Current);
					}
					progressBar.Value = i;
				}
				statusLabel.Text = "Saving image";
				statusLabel.Refresh();
				using (FileStream outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
					using Bitmap bitmap = new Bitmap(MAP_SIZE, MAP_SIZE, MAP_SIZE * 2, PixelFormat.Format16bppArgb1555, imageData);
					bitmap.Save(outputStream, ImageFormat.Png);
				}
				Marshal.FreeHGlobal(imageData);
				statusLabel.Text = "Done!";
				progressBar.Value = 0;
			} catch (Exception e) {
				statusLabel.ForeColor = Color.DarkRed;
				string errorMessage = e switch {
					ArgumentException => "You have to specify a file",
					DirectoryNotFoundException => "Directory doesn't exist",
					FileNotFoundException => "Map file doesn't exist",
					FileFormatException => e.Message,
					NotSupportedException => e.Message,
					_ => "Something went wrong"
				};
				statusLabel.Text = errorMessage;
			}
		}
	}
}
