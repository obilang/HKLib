using HKLib.hk2018;
using System;

public class PredictiveBlockCompressionTest
{
    public static void Main()
    {
        // Create test block
        var block = new PredictiveBlockCompression.Block();

        // Channel 0: Linear increase
        for (int i = 0; i < 16; i++)
            block.Data[0][i] = (short)(1000 + i * 2);

        // Channel 1: Constant
        for (int i = 0; i < 16; i++)
            block.Data[1][i] = 500;

        // Channel 2: Quadratic
        for (int i = 0; i < 16; i++)
            block.Data[2][i] = (short)(i * i);

        // Channels 3-15: Zeros
        for (int ch = 3; ch < 16; ch++)
            for (int fr = 0; fr < 16; fr++)
                block.Data[ch][fr] = 0;

        // Encode
        byte[] compressed = PredictiveBlockCompression.EncodeBlock(block, 16, 16);
        Console.WriteLine($"Original: 512 bytes");
        Console.WriteLine($"Compressed: {compressed.Length} bytes");
        Console.WriteLine($"Ratio: {512.0 / compressed.Length:F2}:1");

        // Decode
        var decoded = PredictiveBlockCompression.DecodeWholeBlock(compressed);

        // Verify
        bool success = true;
        for (int ch = 0; ch < 16; ch++)
        {
            for (int fr = 0; fr < 16; fr++)
            {
                if (block.Data[ch][fr] != decoded.Data[ch][fr])
                {
                    Console.WriteLine($"Mismatch at [{ch}][{fr}]: " +
                        $"{block.Data[ch][fr]} != {decoded.Data[ch][fr]}");
                    success = false;
                }
            }
        }

        Console.WriteLine(success ? "✓ Lossless compression verified!" : "✗ Decompression error");

        // Test single frame decoding
        var frame7 = PredictiveBlockCompression.DecodeSingleFrame(compressed, 7);
        Console.WriteLine($"\nFrame 7 values:");
        for (int ch = 0; ch < 3; ch++)
            Console.WriteLine($"  Channel {ch}: {frame7[ch]}");
    }
}