using System;
using System.IO;
using System.Text;
using System.Threading;
using FileEncodingChecker.Services;

namespace FileEncodingConvertTool.Utils;

public static class FileEncodingConverter
{
    public static string? ConvertFileEncoding(string sourcePath, string sourceEncoding,
        string targetPath, string targetEncoding,
        CancellationToken cancellationToken = default,
        ProgressManager? progress = null)
    {
        progress?.UpdateStatus("验证参数合法性");
        // 参数验证
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentNullException(nameof(sourcePath));
        if (string.IsNullOrEmpty(sourceEncoding))
            throw new ArgumentNullException(nameof(sourceEncoding));
        if (string.IsNullOrEmpty(targetPath))
            throw new ArgumentNullException(nameof(targetPath));
        if (string.IsNullOrEmpty(targetEncoding))
            throw new ArgumentNullException(nameof(targetEncoding));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file not found", sourcePath);

        var sourceEnco = ParseEncoding(sourceEncoding);
        if (sourceEnco == null)
            throw new ArgumentNullException("Invalid source encoding", sourceEncoding);
        var targetEnc = ParseEncoding(targetEncoding);
        
        progress?.UpdateStatus("创建临时文件路径");
        // 创建临时文件路径
        var tempFilePath = Path.Combine(
            Path.GetDirectoryName(targetPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(targetPath) + ".tmp" + Path.GetExtension(targetPath));
        progress?.UpdateStatus($"开始转换文件{Path.GetFileNameWithoutExtension(sourcePath)}");
        try
        {
            // 确保目标目录存在
            var directoryName = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(directoryName) && directoryName != null)
                Directory.CreateDirectory(directoryName);

            using (var sourceStream = File.OpenRead(sourcePath))
            using (var tempStream = File.Create(tempFilePath))
            {
                var totalBytes = sourceStream.Length;
                long processedBytes = 0;

                progress?.InitializeItemCounter(totalBytes);
                // 写入BOM（根据编码自动处理）
                var preamble = targetEnc.GetPreamble();
                if (preamble.Length > 0)
                {
                    tempStream.Write(preamble, 0, preamble.Length);
                }

                // 初始化编解码器
                var decoder = sourceEnco.GetDecoder();
                var encoder = targetEnc.GetEncoder();

                // 缓冲区设置（可根据需要调整大小）
                var inputBuffer = new byte[4096];
                var charBuffer = new char[sourceEnco.GetMaxCharCount(inputBuffer.Length)];
                var outputBuffer = new byte[targetEnc.GetMaxByteCount(charBuffer.Length)];

                var endOfStream = false;
                var inputBytesLeft = 0;

                while (!endOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 填充输入缓冲区
                    if (inputBytesLeft < inputBuffer.Length)
                    {
                        var bytesRead = sourceStream.Read(inputBuffer, inputBytesLeft,
                            inputBuffer.Length - inputBytesLeft);
                        if (bytesRead == 0) endOfStream = true;
                        inputBytesLeft += bytesRead;
                    }

                    // 解码阶段
                    decoder.Convert(inputBuffer, 0, inputBytesLeft, charBuffer, 0, charBuffer.Length,
                        endOfStream, out var bytesUsed, out var charsUsed, out _);

                    // 编码阶段
                    encoder.Convert(charBuffer, 0, charsUsed, outputBuffer, 0, outputBuffer.Length,
                        endOfStream, out _, out var bytesEncoded, out _);

                    // 写入临时文件
                    tempStream.Write(outputBuffer, 0, bytesEncoded);

                    // 处理剩余字节
                    if (bytesUsed < inputBytesLeft)
                    {
                        Buffer.BlockCopy(inputBuffer, bytesUsed, inputBuffer, 0, inputBytesLeft - bytesUsed);
                        inputBytesLeft -= bytesUsed;
                    }
                    else
                    {
                        inputBytesLeft = 0;
                    }

                    // 更新进度
                    processedBytes += bytesUsed;
                    var value = (double)processedBytes / totalBytes * 100;
                    if (progress != null)
                        ((MultiProgressManager)progress).UpdateSubProgress(totalBytes > 0 ? (int)value : 0);
                    if ((int)value == 100)
                    {
                        break;
                    }
                }
                progress?.UpdateStatus($"{Path.GetFileNameWithoutExtension(tempFilePath)} 编码格式转换完成！");
                tempStream.Flush();
                
            }

            // 替换目标文件
            const int maxRetry = 3;
            for (var retry = 0; retry < maxRetry; retry++)
            {
                try
                {
                    if (File.Exists(targetPath))
                    {
                        File.Replace(tempFilePath, targetPath,
                            destinationBackupFileName: null,
                            ignoreMetadataErrors: true);
                    }
                    else
                    {
                        File.Move(tempFilePath, targetPath);
                    }

                    break;
                }
                catch (IOException) when (retry < maxRetry - 1)
                {
                    Thread.Sleep(100 * (retry + 1));
                }
            }
            return targetEncoding;
        }
        catch (OperationCanceledException)
        {
            progress?.UpdateStatus($"用户取消，清理临时文件{Path.GetFileNameWithoutExtension(tempFilePath)}");
            // 清理临时文件
            try
            {
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            }
            catch
            {
                // ignored
            }

            try
            {
                if (File.Exists(sourcePath)) File.Delete(sourcePath);
            }
            catch
            {
                // ignored
            }
        }
        catch
        {
            progress?.UpdateStatus($"转换失败，清理临时文件{Path.GetFileNameWithoutExtension(tempFilePath)}");
            // 清理临时文件
            try
            {
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            }
            catch
            {
                // ignored
            }

            throw;
        }

        return null;
    }

    private static Encoding? ParseEncoding(string encodingName)
    {
        if (string.IsNullOrEmpty(encodingName))
            throw new ArgumentNullException(nameof(encodingName));

        return encodingName.ToLower() switch
        {
            "utf-8" => new UTF8Encoding(false),
            "utf-8-bom" => new UTF8Encoding(true),
            "utf-16" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            "utf-16-bom" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            "utf-16be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
            "utf-16be-bom" => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            _ => GetGetEncoding(encodingName)
        };
    }

    private static Encoding? GetGetEncoding(string encodingName)
    {
        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return null;
    }
}
