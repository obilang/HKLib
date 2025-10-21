using System;
using HKLib.hk2018;
using HKLib.Serialization.hk2018.Binary;

class Program
{
    static void Main(string[] args)
    {
        string hkxPath = @"E:\Git\HavokConvertExperiment\temp\auto_attack1_2018_tag_back.hkt";
        string? compendiumPath = null; // Set if your hkx references one: @"C:\path\to\types.compendium"

        var serializer = new HavokBinarySerializer();

        if (!string.IsNullOrEmpty(compendiumPath))
            serializer.LoadCompendium(compendiumPath);

        // Read the root object
        IHavokObject root = serializer.Read(hkxPath);
        Console.WriteLine($"Root type: {root.GetType().FullName}");

        // If it's a typical Havok container, you can inspect its contents
        if (root is hkRootLevelContainer container)
        {
            foreach (var nv in container.m_namedVariants)
                Console.WriteLine($"{nv.m_name} -> {nv.m_variant?.GetType().Name}");
        }

        // Optional: write back out
        string outPath = hkxPath + ".out.hkx";
        serializer.Write(root, outPath);
        Console.WriteLine($"Wrote: {outPath}");
    }
}