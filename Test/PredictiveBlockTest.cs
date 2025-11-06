using HKLib.hk2018;
using HKLib.Serialization.hk2018.Binary;
using System;
using static HKLib.hk2018.hclStorageSetupMesh;

using System;
using System.Numerics;
using IONET;
using IONET.Core;
using IONET.Core.Animation;
using IONET.Core.Model;
using IONET.Core.Skeleton;

namespace ColladaTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //string hkxPath = @"E:\Git\HavokConvertExperiment\temp\auto_attack1_2018_tag_back.hkt";
            string hkxPath = @"E:\Git\HavokConvertExperiment\temp\a_dmg_wind.hkx";
            string? compendiumPath = null; // Set if your hkx references one: @"C:\path\to\types.compendium"

            var serializer = new HavokBinarySerializer();

            if (!string.IsNullOrEmpty(compendiumPath))
                serializer.LoadCompendium(compendiumPath);

            // Read the root object
            IEnumerable<IHavokObject> havokObjects = serializer.ReadAllObjects(hkxPath);
            var animations = havokObjects.OfType<hkaPredictiveCompressedAnimation>().ToList();
            var animation = animations.FirstOrDefault(); // null if none found
            float[] offsetArray = new float[172];
            Array.Copy(animation.m_floatData.ToArray(), 338, offsetArray, 0, 172);
            float[] scaleArray = new float[172];
            Array.Copy(animation.m_floatData.ToArray(), 510, scaleArray, 0, 172);











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
            //byte[] compressed = PredictiveBlockCompression.EncodeBlock(block, 16, 16);
            byte[] compressed = animation.m_compressedData.ToArray();
            Console.WriteLine($"Original: 512 bytes");
            Console.WriteLine($"Compressed: {compressed.Length} bytes");
            Console.WriteLine($"Ratio: {512.0 / compressed.Length:F2}:1");

            // Decode
            var decoded = PredictiveBlockCompression.DecodeAllFrameChannel(compressed);

            float[][] result = new float[decoded.Length][];

            for (int i = 0; i < decoded.Length; i++)
            {
                for (int j = 0; j < decoded[0].Length; j++)
                {
                    result[i] ??= new float[decoded[0].Length];

                    result[i][j] = ((float)decoded[i][j]) * scaleArray[i] + offsetArray[i];
                }
            }

            //// Verify
            //bool success = true;
            //for (int ch = 0; ch < 16; ch++)
            //{
            //    for (int fr = 0; fr < 16; fr++)
            //    {
            //        if (block.Data[ch][fr] != decoded.Data[ch][fr])
            //        {
            //            Console.WriteLine($"Mismatch at [{ch}][{fr}]: " +
            //                $"{block.Data[ch][fr]} != {decoded.Data[ch][fr]}");
            //            success = false;
            //        }
            //    }
            //}

            //Console.WriteLine(success ? "✓ Lossless compression verified!" : "✗ Decompression error");

            //// Test single frame decoding
            //var frame7 = PredictiveBlockCompression.DecodeSingleFrame(compressed, 7);
            //Console.WriteLine($"\nFrame 7 values:");
            //for (int ch = 0; ch < 3; ch++)
            //    Console.WriteLine($"  Channel {ch}: {frame7[ch]}");



            var skeletons = havokObjects.OfType<hkaSkeleton>().ToList();
            var skeleton = skeletons.FirstOrDefault(); // null if none found


            //// Optional: write back out
            //string outPath = hkxPath + ".out.hkx";
            //serializer.Write(root, outPath);
            //Console.WriteLine($"Wrote: {outPath}");


            var scene = BuildSimpleSkinnedScene(skeleton);
            IOManager.ExportScene(scene, "d:/temp/simple_skeletal.dae", new ExportSettings
            {
                ExportAnimations = true,
                FrameRate = 24.0f,   // used to convert frames -> seconds in DAE
                BlenderMode = true   // improves DAE compatibility for Blender
            });

            Console.WriteLine("Exported d:/temp/simple_skeletal.dae");
        }

        private static IOScene BuildSimpleSkinnedScene(hkaSkeleton khaSkeleton)
        {
            List<IOBone> bones = new List<IOBone>();
            for (int i = 0; i < khaSkeleton.m_bones.Count; i++)
            {
                var bone = khaSkeleton.m_bones[i];
                Console.WriteLine($"Bone {i}: {bone.m_name}");

                var outputBone = new IOBone
                {
                    Name = bone.m_name,
                    Translation = new Vector3(
                        khaSkeleton.m_referencePose[i].m_translation.X,
                        khaSkeleton.m_referencePose[i].m_translation.Y,
                        khaSkeleton.m_referencePose[i].m_translation.Z),
                    Rotation = new Quaternion(
                        khaSkeleton.m_referencePose[i].m_rotation.X,
                        khaSkeleton.m_referencePose[i].m_rotation.Y,
                        khaSkeleton.m_referencePose[i].m_rotation.Z,
                        khaSkeleton.m_referencePose[i].m_rotation.W),
                    Scale = Vector3.One // Havok does not store scale in reference pose
                };

                bones.Insert(i, outputBone);
            }

            var rootBone = null as IOBone;
            // Establish parent-child relationships
            var boneList = bones.ToList();
            for (int i = 0; i < khaSkeleton.m_bones.Count; i++)
            {
                var boneParentIndex = khaSkeleton.m_parentIndices[i];

                if (boneParentIndex < 0)
                {
                    rootBone = boneList[i];
                }

                if (boneParentIndex >= 0)
                {
                    boneList[i].Parent = boneList[boneParentIndex];
                }
            }

            //// Create skeleton: Root -> Bone
            //var root = new IOBone { Name = "Root" };
            //var child = new IOBone { Name = "Bone", Translation = new Vector3(0.5f, 0, 0) };
            //child.Parent = root;

            var skeleton = new IOSkeleton();
            skeleton.RootBones.Add(rootBone);

            // Model with only skeleton (no meshes)
            var model = new IOModel { Name = "Model", Skeleton = skeleton };
            // Ensure no mesh is exported
            model.Meshes.Clear();

            //// Simple Z-rotation animation on the child bone
            //var sceneAnim = new IOAnimation { Name = "ArmatureAnim", StartFrame = 0, EndFrame = 48 };
            //var childGroup = new IOAnimation { Name = child.Name };
            //var rotZ = new IOAnimationTrack(IOAnimationTrackType.RotationEulerZ);
            //rotZ.InsertKeyframe(0, 0f);
            //rotZ.InsertKeyframe(24, MathF.PI / 2f); // 90 degrees
            //rotZ.InsertKeyframe(48, 0f);
            //childGroup.Tracks.Add(rotZ);
            //sceneAnim.Groups.Add(childGroup);

            // Scene with rig + animation only
            var scene = new IOScene { Name = "Scene" };
            scene.Models.Add(model);
            //scene.Animations.Add(sceneAnim);

            return scene;
        }
    }
}