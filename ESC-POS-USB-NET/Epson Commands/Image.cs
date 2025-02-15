using System.Collections;
using System;
using System.IO;
using ESC_POS_USB_NET.Interfaces.Command;
using SkiaSharp;
using ESC_POS_USB_NET.Enums;

namespace ESC_POS_USB_NET.EpsonCommands
{
	public class Image : IImage
	{
		private static BitmapData GetBitmapData(byte[] imageData, bool isScale)
		{
			// Load the image from byte array
			using (SKBitmap originalBmp = SKBitmap.Decode(imageData))
			{
				if (originalBmp == null)
				{
					throw new ArgumentException("Invalid image data.");
				}

				var info = new SKImageInfo(originalBmp.Width, originalBmp.Height);
				using (SKBitmap bmp = new SKBitmap(info))
				{
					using (SKCanvas canvas = new SKCanvas(bmp))
					{
						canvas.Clear(SKColors.White);
						canvas.DrawBitmap(originalBmp, 0, 0);
					}

					double scale = 1.0;
					if (isScale)
					{
						double multiplier = 576; // this depends on your printer model.
						scale = multiplier / bmp.Width;
					}

					int xheight = (int)(bmp.Height * scale);
					int xwidth = (int)(bmp.Width * scale);
					var dimensions = xwidth * xheight;
					var dots = new BitArray(dimensions);
					var threshold = 127; // 127 or 128
					var index = 0;

					for (var y = 0; y < xheight; y++)
					{
						for (var x = 0; x < xwidth; x++)
						{
							var _x = (int)(x / scale);
							var _y = (int)(y / scale);
							var color = bmp.GetPixel(_x, _y);

							// Since the background is white, no need to check alpha
							var luminance = (int)(color.Red * 0.3 + color.Green * 0.59 + color.Blue * 0.11);
							dots[index] = luminance < threshold;
							index++;
						}
					}

					return new BitmapData()
					{
						Dots = dots,
						Height = xheight,
						Width = xwidth
					};
				}
			}
		}

		byte[] IImage.Print(byte[] image, bool isScale, HorizonalAlignment alignment)
		{
			var data = GetBitmapData(image, isScale);
			using (var stream = new MemoryStream())
			using (var bw = new BinaryWriter(stream))
			{
				int offset = 0;
				int widthLowByte = data.Width & 0xFF;
				int widthHighByte = (data.Width >> 8) & 0xFF;
				byte[] width = { (byte)widthLowByte, (byte)widthHighByte };

				// Initialize printer
				bw.Write((char)0x1B);
				bw.Write('@');
				bw.Write((char)0x1B);
				bw.Write('3');
				bw.Write((byte)24);

				if (alignment == HorizonalAlignment.Center)
				{

					bw.Write((char)0x1B);
					bw.Write('a');
					bw.Write((byte)1);
				}
				else if (alignment == HorizonalAlignment.Right)
				{
					bw.Write((char)0x1B);
					bw.Write('a');
					bw.Write((byte)2);
				}
				else
				{
					bw.Write((char)0x1B);
					bw.Write('a');
					bw.Write((byte)0);
				}

				while (offset < data.Height)
				{
					bw.Write((char)0x1B);
					bw.Write('*');         // bit-image mode
					bw.Write((byte)33);    // 24-dot double-density
					bw.Write(width[0]);    // width low byte
					bw.Write(width[1]);    // width high byte

					for (int x = 0; x < data.Width; ++x)
					{
						for (int k = 0; k < 3; ++k)
						{
							byte slice = 0;
							for (int b = 0; b < 8; ++b)
							{
								int y = (((offset / 8) + k) * 8) + b;
								// Calculate the location of the pixel we want in the bit array.
								// It'll be at (y * width) + x.
								int i = (y * data.Width) + x;

								// If the image is shorter than 24 dots, pad with zero.
								bool v = i < data.Dots.Length && data.Dots[i];
								slice |= (byte)((v ? 1 : 0) << (7 - b));
							}

							bw.Write(slice);
						}
					}
					offset += 24;
					bw.Write((char)0x0A);
				}

				// Restore the line spacing to the default of 30 dots.
				bw.Write((char)0x1B);
				bw.Write('3');
				bw.Write((byte)40);

				bw.Flush();
				return stream.ToArray();
			}
		}
	}

	public class BitmapData
	{
		public BitArray Dots { get; set; }
		public int Height { get; set; }
		public int Width { get; set; }
	}
}
