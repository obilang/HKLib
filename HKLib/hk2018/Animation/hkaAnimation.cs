namespace HKLib.hk2018;

public class hkaAnimation : hkReferencedObject
{
    public hkaAnimation.AnimationType m_type;

    public float m_duration;

    public int m_numberOfTransformTracks;

    public int m_numberOfFloatTracks;

    public hkaAnimatedReferenceFrame? m_extractedMotion;

    public List<hkaAnnotationTrack> m_annotationTracks = new();

    public struct TrackAnnotation
    {
        public ushort m_trackID;
        public hkaAnnotationTrack.Annotation m_annotation;
    }

    public enum AnimationType : int
    {
        HK_UNKNOWN_ANIMATION = 0,
        HK_INTERLEAVED_ANIMATION = 1,
        HK_MIRRORED_ANIMATION = 2,
        HK_SPLINE_COMPRESSED_ANIMATION = 3,
        HK_QUANTIZED_COMPRESSED_ANIMATION = 4,
        HK_PREDICTIVE_COMPRESSED_ANIMATION = 5,
        HK_REFERENCE_POSE_ANIMATION = 6
    }

    public AnimationType GetAnimationType()
    {
        return m_type;
    }

    public hkaAnimation()
    {
    }

    public hkaAnimation(hkaAnimation that)
    {
        m_type = that.m_type;
        m_duration = that.m_duration;
        m_numberOfTransformTracks = that.m_numberOfTransformTracks;
        m_numberOfFloatTracks = that.m_numberOfFloatTracks;
        m_extractedMotion = that.m_extractedMotion;
        m_annotationTracks = that.m_annotationTracks;
    }

    // Returns List<List<hkQsTransform>> where:
    // - Outer list index is the frame number
    // - Inner list index is the bone index (as defined by animation binding)
    public virtual List<List<hkQsTransform>> fetchAllTracks()
        => throw new NotSupportedException("fetchAllTracks not implemented for this type of animation.");

    public virtual hkaSkeleton? getSkeleton() => null;

    public virtual void setSkeleton(hkaSkeleton? skeleton) { }
}

