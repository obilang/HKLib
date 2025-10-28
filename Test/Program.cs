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
            string hkxPath = @"E:\Git\HavokConvertExperiment\temp\body.skl";
            string? compendiumPath = null; // Set if your hkx references one: @"C:\path\to\types.compendium"

            var serializer = new HavokBinarySerializer();

            if (!string.IsNullOrEmpty(compendiumPath))
                serializer.LoadCompendium(compendiumPath);

            // Read the root object
            IEnumerable<IHavokObject> havokObjects = serializer.ReadAllObjects(hkxPath);

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