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
using System.Diagnostics;

namespace ColladaTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //string hkxPath = @"E:\Git\HavokConvertExperiment\temp\auto_attack1_2018_tag_back.hkt";
            string skeleton_hkxPath = @"D:\Workspace\FF16\HavokConvertExperiment\temp\body.skl";
            string? compendiumPath = null; // Set if your hkx references one: @"C:\path\to\types.compendium"

            var serializer = new HavokBinarySerializer();

            if (!string.IsNullOrEmpty(compendiumPath))
                serializer.LoadCompendium(compendiumPath);

            // Read the root object
            IEnumerable<IHavokObject> havokObjects = serializer.ReadAllObjects(skeleton_hkxPath);

            var skeletons = havokObjects.OfType<hkaSkeleton>().ToList();
            var skeleton = skeletons.FirstOrDefault(); // null if none found



            // Animation
            string animation_hkxPath = @"D:\GameDev\Resource\FFXVIOut\animation\chara\c1001\animation\a0001\common\normal\jog_f_lp.anmb";

            var serializer2 = new HavokBinarySerializer();

            IEnumerable<IHavokObject> havokObjects2 = serializer2.ReadAllObjects(animation_hkxPath);
            var animations = havokObjects2.OfType<hkaAnimation>().ToList();
            var animation = animations.FirstOrDefault(); // null if none found
            animation.setSkeleton(skeleton);

            var animationBindings = havokObjects2.OfType<hkaAnimationBinding>().ToList();
            var animationBinding = animationBindings.FirstOrDefault(); // null if none found


            var allTracks = animation.fetchAllTracks();



            var scene = BuildSimpleSkinnedScene(skeleton, animationBinding, allTracks);
            IOManager.ExportScene(scene, "d:/temp/simple_skeletal.dae", new ExportSettings
            {
                ExportAnimations = true,
                FrameRate = 24.0f,   // used to convert frames -> seconds in DAE
                BlenderMode = true   // improves DAE compatibility for Blender
            });

            Console.WriteLine("Exported d:/temp/simple_skeletal.fbx");
        }

        private static IOScene BuildSimpleSkinnedScene(hkaSkeleton khaSkeleton, hkaAnimationBinding hkaAnimationBinding, Dictionary<int, List<hkQsTransform>> poseAtTime)
        {
            List<IOBone> bones = new List<IOBone>();
            for (int i = 0; i < khaSkeleton.m_bones.Count; i++)
            {
                var bone = khaSkeleton.m_bones[i];
                Console.WriteLine($"Bone {i}: {bone.m_name}");
                Debug.WriteLine($"Bone {i}: {bone.m_name}");

                var normalizedRotation = Quaternion.Normalize(khaSkeleton.m_referencePose[i].m_rotation);
                var outputBone = new IOBone
                {
                    Name = bone.m_name,
                    Translation = new Vector3(
                        khaSkeleton.m_referencePose[i].m_translation.X,
                        khaSkeleton.m_referencePose[i].m_translation.Y,
                        khaSkeleton.m_referencePose[i].m_translation.Z),
                    Rotation = new Quaternion(
                        normalizedRotation.X,
                        normalizedRotation.Y,
                        normalizedRotation.Z,
                        normalizedRotation.W),
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

            // Simple Z-rotation animation on the child bone
            var sceneAnim = new IOAnimation { Name = "Test", StartFrame = 0, EndFrame = 1 };

            for (int i = 0; i < hkaAnimationBinding.m_transformTrackToBoneIndices.Count; i++)
            {
                int boneIndex = hkaAnimationBinding.m_transformTrackToBoneIndices[i];
                string boneName = khaSkeleton.m_bones[boneIndex].m_name;
                var boneGroup = new IOAnimation { Name = boneName };
                var posXTrack = new IOAnimationTrack(IOAnimationTrackType.PositionX);
                var posYTrack = new IOAnimationTrack(IOAnimationTrackType.PositionY);
                var posZTrack = new IOAnimationTrack(IOAnimationTrackType.PositionZ);
                var rotXTrack = new IOAnimationTrack(IOAnimationTrackType.RotationEulerX);
                var rotYTrack = new IOAnimationTrack(IOAnimationTrackType.RotationEulerY);
                var rotZTrack = new IOAnimationTrack(IOAnimationTrackType.RotationEulerZ);
                var scaleXTrack = new IOAnimationTrack(IOAnimationTrackType.ScaleX);
                var scaleYTrack = new IOAnimationTrack(IOAnimationTrackType.ScaleY);
                var scaleZTrack = new IOAnimationTrack(IOAnimationTrackType.ScaleZ);

                if (!poseAtTime.ContainsKey(boneIndex))
                    continue;

                var poseFrames = poseAtTime[boneIndex];
                for (int f = 0; f < poseFrames.Count; f++)
                {
                    int frame = f;
                    hkQsTransform transform = poseFrames[f];
                    var transform2 = khaSkeleton.m_referencePose[boneIndex]; // For test, use reference pose
                    var normalizedRotation = Quaternion.Normalize(khaSkeleton.m_referencePose[boneIndex].m_rotation);
                    transform2.m_rotation = new Quaternion
                    {
                        X = normalizedRotation.X,
                        Y = normalizedRotation.Y,
                        Z = normalizedRotation.Z,
                        W = normalizedRotation.W
                    };

                    if (Math.Abs(transform.m_rotation.X - transform2.m_rotation.X) > 0.001 ||
                        Math.Abs(transform.m_rotation.Y - transform2.m_rotation.Y) > 0.001 ||
                        Math.Abs(transform.m_rotation.Z - transform2.m_rotation.Z) > 0.001 ||
                        Math.Abs(transform.m_rotation.W - transform2.m_rotation.W) > 0.001
                        )
                    {
                        int test = 0;
                    }


                    float posX = transform.m_translation.X;
                    float posY = transform.m_translation.Y;
                    float posZ = transform.m_translation.Z;
                    posXTrack.InsertKeyframe(frame, posX);
                    posYTrack.InsertKeyframe(frame, posY);
                    posZTrack.InsertKeyframe(frame, posZ);

                    // Convert quaternion to Euler angles (XYZ rotation order)
                    // Reference: https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
                    
                    float sinr_cosp = 2.0f * (transform.m_rotation.W * transform.m_rotation.X + transform.m_rotation.Y * transform.m_rotation.Z);
                    float cosr_cosp = 1.0f - 2.0f * (transform.m_rotation.X * transform.m_rotation.X + transform.m_rotation.Y * transform.m_rotation.Y);
                    float angleX = MathF.Atan2(sinr_cosp, cosr_cosp);

                    // Convert quaternion to Euler Y
                    float sinp = 2.0f * (transform.m_rotation.W * transform.m_rotation.Y - transform.m_rotation.Z * transform.m_rotation.X);
                    float angleY;
                    if (MathF.Abs(sinp) >= 1.0f)
                        angleY = MathF.CopySign(MathF.PI / 2.0f, sinp); // use 90 degrees if out of range
                    else
                        angleY = MathF.Asin(sinp);

                    // Convert quaternion to Euler Z
                    float siny_cosp = 2.0f * (transform.m_rotation.W * transform.m_rotation.Z + transform.m_rotation.X * transform.m_rotation.Y);
                    float cosy_cosp = 1.0f - 2.0f * (transform.m_rotation.Y * transform.m_rotation.Y + transform.m_rotation.Z * transform.m_rotation.Z);
                    float angleZ = MathF.Atan2(siny_cosp, cosy_cosp);
                    // TODO: dae output need to swap x and z rotation?
                    rotXTrack.InsertKeyframe(frame, angleZ);
                    rotYTrack.InsertKeyframe(frame, angleY);
                    rotZTrack.InsertKeyframe(frame, angleX);

                    float scaleX = transform.m_scale.X;
                    if (Math.Abs(transform.m_scale.X - 1) > 0.001)
                    {
                        int test = 0;
                    }
                    float scaleY = transform.m_scale.Y;
                    if (Math.Abs(transform.m_scale.Y - 1) > 0.001)
                    {
                        int test = 0;
                    }
                    float scaleZ = transform.m_scale.Z;
                    if (Math.Abs(transform.m_scale.Z - 1) > 0.001)
                    {
                        int test = 0;
                    }
                    scaleXTrack.InsertKeyframe(frame, scaleX);
                    scaleYTrack.InsertKeyframe(frame, scaleY);
                    scaleZTrack.InsertKeyframe(frame, scaleZ);
                }
                boneGroup.Tracks.Add(posXTrack);
                boneGroup.Tracks.Add(posYTrack);
                boneGroup.Tracks.Add(posZTrack);
                boneGroup.Tracks.Add(scaleXTrack);
                boneGroup.Tracks.Add(scaleYTrack);
                boneGroup.Tracks.Add(scaleZTrack);
                boneGroup.Tracks.Add(rotZTrack);
                boneGroup.Tracks.Add(rotYTrack);
                boneGroup.Tracks.Add(rotXTrack);
                sceneAnim.Groups.Add(boneGroup);
            }

            // Scene with rig + animation only
            var scene = new IOScene { Name = "Scene" };
            scene.Models.Add(model);
            scene.Animations.Add(sceneAnim);

            return scene;
        }
    }
}