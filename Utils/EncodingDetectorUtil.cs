using System;
using System.IO;
using System.Text;
using UtfUnknown;

namespace FileEncodingConvertTool.Utils;

public static class EncodingDetectorUtil
{
    private const int DefaultSampleSize = 4096;
    private const int FastSampleSize = 1024;
    private const int ReliableBufferSize = 4096;

    public static bool IsEnhancedMode = false;
    public static Encoding? SmartDetect(string filePath, out bool whithBom) =>
        DetectWithStrategy(filePath, SampleStrategy.ByExtension, out whithBom);

    public static Encoding? ReliableDetect(string filePath, out bool whithBom) =>
        DetectWithStrategy(filePath, SampleStrategy.Reliable, out whithBom);

    private static Encoding? DetectWithStrategy(string filePath, SampleStrategy strategy, out bool whithBom)
    {
        var (sampleSize, readMethod) = strategy switch
        {
            SampleStrategy.ByExtension => (GetSampleSizeByExtension(filePath), ReadMode.Stream),
            SampleStrategy.Reliable => (ReliableBufferSize, ReadMode.MultiRegion),
            _ => (DefaultSampleSize, ReadMode.Stream)
        };
        return ReadAndDetect(filePath, sampleSize, readMethod, out whithBom);
    }

    private static int GetSampleSizeByExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".txt" => 2048,
            ".csv" => 4096,
            ".log" => 8192,
            _ => DefaultSampleSize
        };
    }

    private static Encoding? ReadAndDetect(string filePath, int sampleSize, ReadMode mode, out bool whithBom)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var buffer = mode switch
        {
            ReadMode.MultiRegion => ReadMultiRegion(fs, sampleSize),
            _ => ReadStreamHeader(fs, sampleSize)
        };
        // 优先检测BOM
        var bomEncoding = CheckBomEncoding(buffer);

        whithBom = bomEncoding != null;
        if (whithBom)
        {
            return bomEncoding;
        }

        var resultEncoding = DetectFromBuffer(buffer);
        if (!IsEnhancedMode && resultEncoding == null)
        {
            return resultEncoding;
        }
        // 增强模式：尝试用其他方式检测
        return  DetectFromFilePath(filePath);
    }

    private static byte[] ReadMultiRegion(FileStream fs, int bufferSize)
    {
        var buffer = new byte[bufferSize];

        // 头部样本
        fs.Read(buffer, 0, FastSampleSize);

        // 尾部样本（处理小文件情况）
        var tailStart = Math.Max(0, (int)fs.Length - (bufferSize - FastSampleSize));
        fs.Seek(tailStart, SeekOrigin.Begin);
        fs.Read(buffer, FastSampleSize, bufferSize - FastSampleSize);

        return buffer;
    }

    private static byte[] ReadStreamHeader(Stream stream, int maxBytes)
    {
        var size = Math.Min(stream.Length, maxBytes);
        var buffer = new byte[size];
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(buffer, 0, (int)size);
        return buffer;
    }

    private static Encoding? DetectFromBuffer(byte[] buffer)
    {
        try
        {
            var result = CharsetDetector.DetectFromBytes(buffer);
            return MapEncoding(result.Detected);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    private static Encoding? DetectFromFilePath(string filePath)
    {
        try
        {
            var result = CharsetDetector.DetectFromFile(filePath);
            return result?.Detected?.Encoding;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    private static Encoding? CheckBomEncoding(byte[] buffer)
    {
        if (buffer.Length >= 4)
        {
            // UTF-32 BE
            if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                return Encoding.GetEncoding(12001);
            // UTF-32 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                return Encoding.UTF32;
        }

        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return Encoding.UTF8;

        if (buffer.Length >= 2)
        {
            // UTF-16 BE
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            // UTF-16 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode;
        }

        return null;
    }

    private static Encoding? MapEncoding(DetectionDetail? detail)
    {
        if (detail == null) return null;
        return detail.EncodingName.ToUpper() switch
        {
            "GB18030" => Encoding.GetEncoding("GB18030"),
            "GB2312" => Encoding.GetEncoding(20936),
            "GBK" => Encoding.GetEncoding(936),
            "UTF-16BE" => Encoding.BigEndianUnicode,
            "UTF-16" => Encoding.Unicode,
            "WINDOWS-1252" => Encoding.GetEncoding(1252),
            "UTF-8" => Encoding.UTF8,
            _ => detail.Encoding ?? Encoding.UTF8
        };
    }

    private enum SampleStrategy
    {
        ByExtension,
        Reliable
    }

    private enum ReadMode
    {
        Stream,
        MultiRegion
    }
}