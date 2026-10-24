// -------------------------------------------------------------------------------------------------------------------------------------------------------------
// Author: 3dapi (https://github.com/3dapi)
// -------------------------------------------------------------------------------------------------------------------------------------------------------------

using Vortice.Direct2D1;
using Vortice.WIC;

static class G2TextureLoader
{
	private static IWICImagingFactory? _wicFactory;
	private static IWICImagingFactory WicFactory
	{
		get
		{
			_wicFactory ??= new IWICImagingFactory();
			return _wicFactory;
		}
	}

	public static ID2D1Bitmap? LoadBitmap(string filePath, bool grayscale = false)
	{
		var renderTarget = G2AppBase.Instance?.RenderTarget?? throw new InvalidOperationException("ID2D1Bitmap::G2AppBase instance is not initialized.");
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(filePath));
		}
		if (!File.Exists(filePath))
		{
			return null;
		}
		using IWICBitmapDecoder decoder = WicFactory.CreateDecoderFromFileName(filePath);
		using IWICBitmapFrameDecode frame = decoder.GetFrame(0);
		using IWICFormatConverter converter = WicFactory.CreateFormatConverter();
		converter.Initialize(frame, PixelFormat.Format32bppPBGRA);
		if (grayscale)
		{
			var size = converter.Size;
			uint stride = checked((uint)size.Width * 4);
			byte[] pixels = new byte[checked((int)(stride * size.Height))];
			converter.CopyPixels(stride, pixels);
			// 투명도는 유지하고 빨강·초록·파랑을 같은 밝기로 바꿉니다.
			for (int i = 0; i < pixels.Length; i += 4)
			{
				byte gray = (byte)((pixels[i] * 114 + pixels[i + 1] * 587 + pixels[i + 2] * 299 + 500) / 1000);
				pixels[i] = pixels[i + 1] = pixels[i + 2] = gray;
			}
			var pin = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
			try
			{
				return renderTarget.CreateBitmap(size, pin.AddrOfPinnedObject(), stride,
					new BitmapProperties(new Vortice.DCommon.PixelFormat(
						Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
			}
			finally { pin.Free(); }
		}
		return renderTarget.CreateBitmapFromWicBitmap(converter);
	}

	public static void Release()
	{
		_wicFactory?.Dispose();
		_wicFactory = null;
	}
}
