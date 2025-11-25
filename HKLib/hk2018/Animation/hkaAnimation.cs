namespace HKLib.hk2018;

public partial class hkaAnimation : hkReferencedObject
{
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

