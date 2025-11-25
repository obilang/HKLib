using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HKLib.hk2018;

public class hkaPredictiveCompressedAnimation : hkaAnimation
{
    public List<byte> m_compressedData = new();

    public List<ushort> m_intData = new();

    public readonly int[] m_intArrayOffsets = new int[9];

    public List<float> m_floatData = new();

    public readonly int[] m_floatArrayOffsets = new int[3];

    public int m_numBones;

    public int m_numFloatSlots;

    public int m_numFrames;

    public int m_firstFloatBlockScaleAndOffsetIndex;

    public class TrackCompressionParams : IHavokObject
    {
        public float m_staticTranslationTolerance;

        public float m_staticRotationTolerance;

        public float m_staticScaleTolerance;

        public float m_staticFloatTolerance;

        public float m_dynamicTranslationTolerance;

        public float m_dynamicRotationTolerance;

        public float m_dynamicScaleTolerance;

        public float m_dynamicFloatTolerance;

    }

    private hkaSkeleton? _skeleton;


    private enum IntArrayID
    {
        BLOCK_OFFSETS,
        FIRST_FLOAT_BLOCK_OFFSETS,
        IS_ANIMATED_BITMAP,
        IS_FIXED_RANGE_BITMAP,
        DYNAMIC_BONE_TRACK_INDEX,
        DYNAMIC_FLOAT_TRACK_INDEX,
        STATIC_BONE_TRACK_INDEX,
        STATIC_FLOAT_TRACK_INDEX,
        RENORM_QUATERNION_INDEX,
        NUM_INT_ARRAYS
    }

    private enum FloatArrayID
    {
        STATIC_VALUES,
        DYNAMIC_SCALES,
        DYNAMIC_OFFSETS,
        NUM_FLOAT_ARRAYS
    }


    private ReadOnlySpan<ushort> getArray(IntArrayID x) => CollectionsMarshal.AsSpan(m_intData).Slice(m_intArrayOffsets[(int)x], getArrayLength(x));

    private ReadOnlySpan<float> getArray(FloatArrayID x) => CollectionsMarshal.AsSpan(m_floatData).Slice(m_floatArrayOffsets[(int)x], getArrayLength(x));

    private int getArrayLength(IntArrayID x)
    {
        const int EXTRA_ELEMS = 8;
        int start = m_intArrayOffsets[(int)x];
        int end = (x == IntArrayID.NUM_INT_ARRAYS - 1) ? (m_intData.Count - EXTRA_ELEMS) : m_intArrayOffsets[(int)x + 1];
        return end - start;
    }

    private int getArrayLength(FloatArrayID x)
    {
        const int EXTRA_ELEMS = 4;
        int start = m_floatArrayOffsets[(int)x];
        int end = (x == FloatArrayID.NUM_FLOAT_ARRAYS - 1) ? (m_floatData.Count - EXTRA_ELEMS) : m_floatArrayOffsets[(int)x + 1];
        return end - start;
    }

    private static void applyWeights(ReadOnlySpan<ushort> bitmap, Span<byte> weights, int n)
    {
        if (weights.Length > 0)
        {
            for (int i = 0; i < n; i += 16)
            {
                int isAnimated = bitmap[i / 16];
                int numWeights = Math.Min(n - i, 16);
                for (int j = 0; j < numWeights; j++)
                {
                    weights[i + j] &= (byte)(isAnimated & 1);
                    isAnimated >>= 1;
                }
            }
        }
    }


    private static void quaternionRecoverW(ref Quaternion v, bool usingManhattan = true)
    {
        float w;
        if (usingManhattan)
        {
            float sum = MathF.Abs(v.X) + MathF.Abs(v.Y) + MathF.Abs(v.Z);
            w = 1f - sum;
            if (w < 0f) w = 0f;
            else if (w > 1f) w = 1f;
        }
        else
        {
            // Euclidean approach: w = sqrt(1 - x^2 - y^2 - z^2)
            float lengthSquared = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            float wSquared = Math.Clamp(1.0f - lengthSquared, 0.0f, 1.0f);
            w = MathF.Sqrt(wSquared);
        }
        v = new Quaternion(v.X, v.Y, v.Z, w);
    }

    public hkaPredictiveCompressedAnimation()
    {
        m_type = AnimationType.HK_PREDICTIVE_COMPRESSED_ANIMATION;
    }


    // Set associated skeleton (validates track counts when available)
    public override void setSkeleton(hkaSkeleton skeleton)
    {
        if (skeleton is null) throw new ArgumentNullException(nameof(skeleton));
        if (skeleton.m_bones.Count != m_numBones)
            throw new ArgumentException("Number of skeleton bones does not match animation", nameof(skeleton));
        if (skeleton.m_floatSlots.Count != m_numFloatSlots)
            throw new ArgumentException("Number of skeleton float slots does not match animation", nameof(skeleton));

        _skeleton = skeleton;
    }

    protected void setBoneChannelVal(ref hkQsTransform bone, int floatIndex, float value)
    {
        switch (floatIndex)
        {
            case 0: bone.m_translation.X = value; break;
            case 1: bone.m_translation.Y = value; break;
            case 2: bone.m_translation.Z = value; break;
            case 4: bone.m_rotation.X = value; break;
            case 5: bone.m_rotation.Y = value; break;
            case 6: bone.m_rotation.Z = value; break;
            //case 7: bone.m_rotation.W = value; break;
            case 8: bone.m_scale.X = value; break;
            case 9: bone.m_scale.Y = value; break;
            case 10: bone.m_scale.Z = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(floatIndex), "Invalid float index for hkQsTransform");
        }
    }

    public override List<List<hkQsTransform>> fetchAllTracks()
    {
        Dictionary<int, List<hkQsTransform>> allTracks = new();
        var refBones = _skeleton?.m_referencePose.ToArray() ?? Array.Empty<hkQsTransform>();
        // prepare needed data
        ReadOnlySpan<float> scalePtr = getArray(FloatArrayID.DYNAMIC_SCALES);
        ReadOnlySpan<float> offsetPtr = getArray(FloatArrayID.DYNAMIC_OFFSETS);
        ReadOnlySpan<ushort> isAnimated = getArray(IntArrayID.IS_ANIMATED_BITMAP);
        ReadOnlySpan<ushort> isFixedRange = getArray(IntArrayID.IS_FIXED_RANGE_BITMAP);

        if (m_numFloatSlots > 0)
        {
            Console.WriteLine("This animation has float tracks. Might cause some issue since we haven't implement the float track decode");
        }

        var boneWeights = new byte[m_numBones];
        var floatWeights = new byte[m_numFloatSlots];
        Array.Fill(boneWeights, (byte)0xff);
        Array.Fill(floatWeights, (byte)0xff);
        applyWeights(isAnimated, boneWeights, m_numBones);
        applyWeights(isAnimated.Slice((m_numBones + 15) / 16), floatWeights, m_numFloatSlots);

        // Sample bones
        if (m_numBones > 0)
        {
            int numFloatsPerBone = m_numBones * 3 * 4; // translation(3) + rotation(4) + scale(3) = 10, but stored as 12 floats

            for (int i = 0; i < m_numBones; i++)
            {
                // skip unanimated bones
                if (boneWeights[i] == 0)
                    continue;

                List<hkQsTransform> boneFrames = new();
                allTracks[i] = boneFrames;

                for (int f = 0; f < m_numFrames; f++)
                {
                    // set the transform to reference pose by default
                    var boneTransform = new hkQsTransform();
                    boneTransform.m_translation = refBones[i].m_translation;
                    boneTransform.m_rotation = refBones[i].m_rotation;
                    boneTransform.m_scale = refBones[i].m_scale;
                    boneFrames.Add(boneTransform);
                }
            }

            List<int> boneNeedRecoverW = new List<int>();
            // Copy static values
            ReadOnlySpan<ushort> staticIdxArray = getArray(IntArrayID.STATIC_BONE_TRACK_INDEX);
            int nstatic = getArrayLength(IntArrayID.STATIC_BONE_TRACK_INDEX);
            if (nstatic > 0)
            {
                ReadOnlySpan<float> staticVals = getArray(FloatArrayID.STATIC_VALUES);

                for (int i = 0; i < nstatic && staticIdxArray[i] < numFloatsPerBone; i++)
                {
                    int channelIdx = staticIdxArray[i];
                    float v = staticVals[i];

                    // find ref bone index
                    int boneIndex = channelIdx / 12;
                    if (!allTracks.ContainsKey(boneIndex))
                        continue;

                    for (int f = 0; f < m_numFrames; f++)
                    {
                        var boneFrame = allTracks[boneIndex][f];
                        setBoneChannelVal(ref boneFrame, channelIdx % 12, v);
                        //quaternionRecoverW(ref boneFrame.m_rotation);
                        allTracks[boneIndex][f] = boneFrame;
                    }

                    if (channelIdx % 12 >= 4 && channelIdx % 12 <= 6)
                    {
                        // rotation channel, need recover W later
                        if (boneNeedRecoverW.Contains(boneIndex) == false)
                            boneNeedRecoverW.Add(boneIndex);
                    }
                }
            }


            // Copy dynamic values
            int ndynamic = getArrayLength(IntArrayID.DYNAMIC_BONE_TRACK_INDEX);
            ReadOnlySpan<ushort> dynamicIdx = getArray(IntArrayID.DYNAMIC_BONE_TRACK_INDEX);
            bool hasFixedRange = false;
            if (ndynamic > 0)
            {
                ReadOnlySpan<ushort> blockOffsets = getArray(IntArrayID.BLOCK_OFFSETS);
                // decode the m_compressedData
                var dynamicValChannelFrame = PredictiveBlockCompression.DecodeAllFrameChannel(m_compressedData.ToArray(), blockOffsets.ToArray(), ndynamic, m_numFrames);

                // Fixed scale and offset for fixed-range channels
                float fixedScale = 1.0f / ((1 << 13) - 1);  // 1.0 / 8191
                float fixedOffset = 0.0f;

                int scaleOffsetIndex = 0;  // Index into scale/offset arrays
                for (int i = 0; i < ndynamic && dynamicIdx[i] < numFloatsPerBone; i++)
                {
                    int channelIdx = dynamicIdx[i];

                    // Check if this channel uses fixed range (every 16 channels share one ushort in isFixedRange)
                    int bitmapIndex = i / 16;
                    int bitPosition = i % 16;
                    bool useFixedRange = ((isFixedRange[bitmapIndex] >> bitPosition) & 1) != 0;

                    float scale, offset;
                    if (useFixedRange)
                    {
                        // Use fixed scale/offset for this channel
                        scale = fixedScale;
                        offset = fixedOffset;
                        hasFixedRange = true;
                    }
                    else
                    {
                        // Use dynamic scale/offset from arrays
                        scale = scalePtr[scaleOffsetIndex];
                        offset = offsetPtr[scaleOffsetIndex];
                        scaleOffsetIndex++;
                    }

                    // find ref bone index
                    int boneIndex = channelIdx / 12;
                    if (!allTracks.ContainsKey(boneIndex))
                        continue;

                    for (int f = 0; f < m_numFrames; f++)
                    {
                        var boneFrame = allTracks[boneIndex][f];
                        setBoneChannelVal(ref boneFrame, channelIdx % 12, dynamicValChannelFrame[i][f] * scale + offset);
                        //quaternionRecoverW(ref boneFrame.m_rotation);
                        allTracks[boneIndex][f] = boneFrame;
                    }

                    if (channelIdx % 12 >= 4 && channelIdx % 12 <= 6)
                    {
                        // rotation channel, need recover W later
                        if (boneNeedRecoverW.Contains(boneIndex) == false)
                            boneNeedRecoverW.Add(boneIndex);
                    }
                }
            }

            if (hasFixedRange)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                // For me to notice during testing. Didn't find one in my test files yet.
                Console.WriteLine("This animation has fixed range");
                Console.ResetColor();
            }


            // Recover and interpolate w components of quaternions
            for (int i = 0; i < boneNeedRecoverW.Count; i++)
            {
                int boneIndex = boneNeedRecoverW[i];
                if (!allTracks.ContainsKey(boneIndex))
                    continue;
                for (int f = 0; f < m_numFrames; f++)
                {
                    var boneFrame = allTracks[boneIndex][f];
                    quaternionRecoverW(ref boneFrame.m_rotation);
                    allTracks[boneIndex][f].m_rotation = Quaternion.Normalize(boneFrame.m_rotation);
                }
            }

            // There is data for renormalizing quaternions, but since we have already recovered W, we can skip it
            //ReadOnlySpan<ushort> normQuaternions = getArray(IntArrayID.RENORM_QUATERNION_INDEX);
            //int nquats = getArrayLength(IntArrayID.RENORM_QUATERNION_INDEX);
            //if (nquats > 0)
        }


        List<List<hkQsTransform>> convertedTracks = new List<List<hkQsTransform>>();

        for (int i = 0; i < m_numFrames; i++)
        {
            List<hkQsTransform> frameTransforms = new List<hkQsTransform>();

            foreach (var kvp in allTracks)
            {
                frameTransforms.Add(kvp.Value[i]);
            }
            convertedTracks.Add(frameTransforms);
        }

        return convertedTracks;
    }
}


