using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace _7DTDMapExtractor {
	public static class MapExtractor {
		private const string MAGIC = "map\0";
		// Just set to the size of Navezgane for now, but should be easily changeable in the future
		private const int MAP_SIZE = 6144;
		private const int MAX_CHUNK = MAP_SIZE / 16 / 2;

		public static void Extract(string mapFile, string outputFile, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			IntPtr imageDataPtr = IntPtr.Zero;
			try {
				using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				using BinaryReader reader = new BinaryReader(inputStream);
				/*  ---------------------  */
				/*  Image Data Allocation  */
				/*  ---------------------  */
				imageDataPtr = Marshal.AllocHGlobal(MAP_SIZE * MAP_SIZE * sizeof(ushort));
				Span<ushort> imageData;
				unsafe { imageData = new Span<ushort>(imageDataPtr.ToPointer(), MAP_SIZE * MAP_SIZE); }
				imageData.Clear();
				/*  ------  */
				/*  Header  */
				/*  ------  */
				updateStatus?.Invoke("Reading header");
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
				/*  -----------------  */
				/*  Chunk Coordinates  */
				/*  -----------------  */
				updateStatus?.Invoke("Reading chunk coordinates");
				newProgressStage?.Invoke(numChunks - 1);
				uint[] chunkCoordinates = new uint[numChunks];
				for (int i = 0; i < numChunks; i++) {
					chunkCoordinates[i] = reader.ReadUInt32();
					updateProgress?.Invoke(i);
				}
				// Unused space
				inputStream.Seek(0x10 + maxChunks * 4, SeekOrigin.Begin);
				/*  -----------  */
				/*  Colour Data  */
				/*  -----------  */
				updateStatus?.Invoke("Reading colour data and painting image");
				newProgressStage?.Invoke(numChunks - 1);
				for (int i = 0; i < numChunks; i++) {
					int cx = (short)(chunkCoordinates[i] & 0xFFFFU);
					int cy = (short)(chunkCoordinates[i] >> 16);
					if (cx >= -MAX_CHUNK && cx < MAX_CHUNK && cy >= -MAX_CHUNK && cy < MAX_CHUNK) {
						for (int y = 0; y < 16; y++) {
							for (int x = 0; x < 16; x++) {
								ushort colour = reader.ReadUInt16();
								int index = (x + cx * 16 + MAP_SIZE / 2) + (MAP_SIZE - 1 - (y + cy * 16 + MAP_SIZE / 2)) * MAP_SIZE;
								imageData[index] = (ushort)(colour | 0x8000);
							}
						}
					} else {
						inputStream.Seek(512, SeekOrigin.Current);
					}
					updateProgress?.Invoke(i);
				}
				updateStatus?.Invoke("Saving image");
				using (FileStream outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
					using Bitmap bitmap = new Bitmap(MAP_SIZE, MAP_SIZE, MAP_SIZE * sizeof(ushort), PixelFormat.Format16bppArgb1555, imageDataPtr);
					bitmap.Save(outputStream, ImageFormat.Png);
				}
				updateStatus?.Invoke("Done!");
				newProgressStage?.Invoke(1);
			} finally {
				// Make sure to free the image data, no matter what
				// In case nothing was ever allocated, this'll just do nothing
				Marshal.FreeHGlobal(imageDataPtr);
			}
		}
	}
}
