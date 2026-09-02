using UnityEngine;
using UnityEngine.U2D.Animation;

[ExecuteAlways]
public class SpriteVisualAnimationTest : MonoBehaviour
{
    [SerializeField]
    private TestVisualPose pose = TestVisualPose.Default;

    [SerializeField]
    private string category = "torso";

    [SerializeField]
    private string defaultLabel = "Default";

    [SerializeField]
    private string readyLabel = "CounterReady";

    [SerializeField]
    private string attackLabel = "CounterAttack";

    [SerializeField]
    private int defaultSortingOrder;

    [SerializeField]
    private int attackSortingOrder = 10;

    private SpriteResolver _resolver;
    private SpriteRenderer _renderer;

    public TestVisualPose Pose => pose;

    private void OnEnable()
    {
        CacheComponents();
        Apply();
    }

    private void OnValidate()
    {
        CacheComponents();
        Apply();
    }

    private void OnDidApplyAnimationProperties()
    {
        CacheComponents();
        Apply();
    }

    private void CacheComponents()
    {
        if (_resolver == null)
            _resolver = GetComponent<SpriteResolver>();

        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }

    private void Apply()
    {
        ApplyPose(pose);
    }

    public void PreviewPose(
        TestVisualPose previewPose)
    {
        CacheComponents();
        ApplyPose(previewPose);
    }

    private void ApplyPose(
        TestVisualPose targetPose)
    {
        if (_resolver == null)
            return;

        string label = targetPose switch
        {
            TestVisualPose.Default =>
                defaultLabel,

            TestVisualPose.CounterReady =>
                readyLabel,

            TestVisualPose.CounterAttack =>
                attackLabel,

            _ =>
                defaultLabel
        };

        _resolver.SetCategoryAndLabel(
            category,
            label);

        if (_renderer == null)
            return;

        _renderer.sortingOrder =
            targetPose == TestVisualPose.CounterAttack
                ? attackSortingOrder
                : defaultSortingOrder;
    }
}

public enum TestVisualPose
{
    Default = 0,
    CounterReady = 1,
    CounterAttack = 2
}
