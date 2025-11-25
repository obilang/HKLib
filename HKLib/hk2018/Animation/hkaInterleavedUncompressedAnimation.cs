namespace HKLib.hk2018;

public partial class hkaInterleavedUncompressedAnimation : hkaAnimation
{
    private hkaSkeleton? _skeleton;

    public hkaInterleavedUncompressedAnimation()
    {
        m_type = AnimationType.HK_INTERLEAVED_ANIMATION;
    }

    // Set associated skeleton (validates track counts when available)
    public override void setSkeleton(hkaSkeleton? skeleton)
    {
        if (skeleton is null)
        {
            _skeleton = null;
            return;
        }

        //if (skeleton.m_bones.Count != m_numberOfTransformTracks)
        //    throw new ArgumentException("Number of skeleton bones does not match animation transform tracks", nameof(skeleton));

        if (skeleton.m_floatSlots.Count != m_numberOfFloatTracks)
            throw new ArgumentException("Number of skeleton float slots does not match animation float tracks", nameof(skeleton));

        _skeleton = skeleton;
    }

    public override hkaSkeleton? getSkeleton() => _skeleton;

    public override List<List<hkQsTransform>> fetchAllTracks()
    {
        List<List<hkQsTransform>> allTracks = new();

        if (m_numberOfTransformTracks == 0 || m_transforms.Count == 0)
        {
            return allTracks;
        }

        // Calculate number of frames from the total transforms and track count
        int frameCount = m_transforms.Count / m_numberOfTransformTracks;

        if (m_transforms.Count % m_numberOfTransformTracks != 0)
        {
            throw new InvalidOperationException(
                $"Invalid transform data: {m_transforms.Count} transforms is not evenly divisible by {m_numberOfTransformTracks} tracks");
        }

        // Initialize frames: outer list = frames, inner list = bones per frame
        for (int frame = 0; frame < frameCount; frame++)
        {
            List<hkQsTransform> bonesAtFrame = new(m_numberOfTransformTracks);

            // Data is stored interleaved: [frame0_track0, frame0_track1, ..., frame1_track0, frame1_track1, ...]
            for (int track = 0; track < m_numberOfTransformTracks; track++)
            {
                int index = frame * m_numberOfTransformTracks + track;
                bonesAtFrame.Add(m_transforms[index]);
            }

            allTracks.Add(bonesAtFrame);
        }

        return allTracks;
    }
}
