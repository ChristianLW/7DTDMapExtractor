using System;
using System.IO;
using System.IO.Compression;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace _7DTDMapExtractor {
	public static class MapExtractor {
		private const string MAGIC = "map\0";
		// Just set to the size of Navezgane for now, but should be easily changeable in the future
		private const int MAP_SIZE = 6144;
		private const int CHUNKS_IN_REGION = 32;

		public static void Extract(string mapFile, string outputFile, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			IntPtr imageDataPtr = IntPtr.Zero;
			try {
				/*  ---------------------  */
				/*  Image Data Allocation  */
				/*  ---------------------  */
				imageDataPtr = Marshal.AllocHGlobal(MAP_SIZE * MAP_SIZE * sizeof(ushort));
				Span<ushort> imageData;
				unsafe { imageData = new Span<ushort>(imageDataPtr.ToPointer(), MAP_SIZE * MAP_SIZE); }
				imageData.Clear();
				/*  --------  */
				/*  Map File  */
				/*  --------  */
				if (Path.GetExtension(mapFile) == ".7rm") {
					ExtractRegionMapFiles(Path.GetDirectoryName(mapFile), imageData, updateProgress, newProgressStage, updateStatus);
				} else {
					ExtractMapFile(mapFile, imageData, updateProgress, newProgressStage, updateStatus);
				}
				/*  -----------  */
				/*  Final Stuff  */
				/*  -----------  */
				updateStatus?.Invoke("Saving image");
				SaveImage(outputFile, imageDataPtr);
				updateStatus?.Invoke("Done!");
				newProgressStage?.Invoke(1);
			} finally {
				// Make sure to free the image data, no matter what
				// In case nothing was ever allocated, this'll just do nothing
				Marshal.FreeHGlobal(imageDataPtr);
			}
		}

		private static void SaveImage(string outputFile, IntPtr imageDataPtr) {
			// This function only exists because you can't prevent Bitmap from writing a few extra PNG chunks
			// including physical size, which doesn't make any sense for an image like this
			byte[] buffer;
			int length;
			// Give it an initial capacity of 256k to avoid a bunch of tiny reallocations
			using (MemoryStream memStream = new MemoryStream(256 * 1024)) {
				using Bitmap bitmap = new Bitmap(MAP_SIZE, MAP_SIZE, MAP_SIZE * sizeof(ushort), PixelFormat.Format16bppArgb1555, imageDataPtr);
				bitmap.Save(memStream, ImageFormat.Png);
				buffer = memStream.GetBuffer();
				length = (int)memStream.Length;
			}
			using FileStream outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
			// Start by writing the PNG signature
			outputStream.Write(buffer, 0, 8);
			int pos = 8;
			while (pos < length) {
				int chunkLength = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
				chunkLength += 12;
				// Only write critical chunks
				if (((buffer[pos + 4] >> 5) & 1) == 0) {
					outputStream.Write(buffer, pos, chunkLength);
				}
				pos += chunkLength;
			}
		}

		private static void PaintChunk(int cx, int cy, BinaryReader reader, Span<ushort> imageData) {
			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					ushort colour = reader.ReadUInt16();
					int index = (x + cx * 16 + MAP_SIZE / 2) + (MAP_SIZE - 1 - (y + cy * 16 + MAP_SIZE / 2)) * MAP_SIZE;
					imageData[index] = (ushort)(colour | 0x8000);
				}
			}
		}

		private static void ExtractMapFile(string mapFile, Span<ushort> imageData, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using BinaryReader reader = new BinaryReader(inputStream);
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
			int MAX_CHUNK = MAP_SIZE / 16 / 2;
			for (int i = 0; i < numChunks; i++) {
				int cx = (short)(chunkCoordinates[i] & 0xFFFFU);
				int cy = (short)(chunkCoordinates[i] >> 16);
				if (cx >= -MAX_CHUNK && cx < MAX_CHUNK && cy >= -MAX_CHUNK && cy < MAX_CHUNK) {
					PaintChunk(cx, cy, reader, imageData);
				} else {
					inputStream.Seek(512, SeekOrigin.Current);
				}
				updateProgress?.Invoke(i);
			}
		}

		private static void ExtractRegionMapFiles(string mapDir, Span<ushort> imageData, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			List<string> files = new List<string>(Directory.EnumerateFiles(mapDir, "*.7rm"));
			updateStatus?.Invoke("Reading colour data and painting image");
			newProgressStage?.Invoke(files.Count);
			int progress = 0;
			// Using a buffer both for performance and for being able to check whether a chunk should be drawn
			const int bytesPerChunk = 16 * 16 * sizeof(ushort);
			byte[] buffer = new byte[bytesPerChunk * CHUNKS_IN_REGION];
			using MemoryStream memStream = new MemoryStream(buffer);
			using BinaryReader reader = new BinaryReader(memStream);
			foreach (string mapFile in files) {
				/*  -----------------  */
				/*  Chunk Coordinates  */
				/*  -----------------  */
				ReadOnlySpan<char> fileName = Path.GetFileName(mapFile.AsSpan());
				int cx, cy;
				try {
					if (!fileName.StartsWith("r.")) throw new Exception();
					ReadOnlySpan<char> nameSpan = fileName.Slice("r.".Length, fileName.Length - "r..7rm".Length);
					int separator = nameSpan.IndexOf('.');
					cx = int.Parse(nameSpan.Slice(0, separator), NumberStyles.AllowLeadingSign);
					cy = int.Parse(nameSpan.Slice(separator + 1), NumberStyles.AllowLeadingSign);
				} catch {
					throw new FileFormatException($"File name \"{fileName}\" doesn't match required pattern");
				}
				cx *= CHUNKS_IN_REGION;
				cy *= CHUNKS_IN_REGION;
				/*  -----------  */
				/*  Actual File  */
				/*  -----------  */
				using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				int version;
				using (BinaryReader versionReader = new BinaryReader(inputStream, Encoding.UTF8, true)) {
					version = versionReader.ReadInt32();
					if (version != 1) {
						throw new NotSupportedException($"Unsupported file format version ({version}) in file {fileName}");
					}
				}
				/*  -----------  */
				/*  Colour Data  */
				/*  -----------  */
				using GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
				for (int y = 0; y < CHUNKS_IN_REGION; y++) {
					memStream.Position = 0;
					gzipStream.ReadExactly(buffer);
					for (int x = 0; x < CHUNKS_IN_REGION; x++) {
						if (new ReadOnlySpan<byte>(buffer, x * bytesPerChunk, bytesPerChunk).ContainsAnyExcept((byte)0)) {
							PaintChunk(cx + x, cy + y, reader, imageData);
						} else {
							memStream.Position += bytesPerChunk;
						}
					}
				}
				// Only bother updating after a whole region, because otherwise the program
				// spends more time updating the progress bar than actually doing stuff
				updateProgress?.Invoke(++progress);
			}
		}
	}
}
