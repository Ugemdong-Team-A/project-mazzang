public enum SkillSlot : byte
{
    Skill1 = 0,
    Skill2 = 1
}

public abstract class Skill
{
    public SkillData Data
    {
        get;
        private set;
    }

    public SkillSlot Slot
    {
        get;
        private set;
    }

    protected PlayerSkillController Controller
    {
        get;
        private set;
    }

    protected PlayerContext Context
    {
        get;
        private set;
    }

    public bool IsInitialized
    {
        get;
        private set;
    }


    public void Initialize(
        SkillData data,
        SkillSlot slot,
        PlayerSkillController controller,
        PlayerContext context)
    {
        Data =
            data;

        Slot =
            slot;

        Controller =
            controller;

        Context =
            context;

        IsInitialized =
            true;

        OnInitialized();
    }


    protected virtual void OnInitialized()
    {
    }


    public virtual bool CanUse(
        in SkillUseContext useContext)
    {
        return IsInitialized;
    }


    public abstract void Activate(
        in SkillUseContext useContext);


    public virtual void FixedUpdateNetwork()
    {
    }


    public virtual void Render()
    {
    }


    public virtual void Cancel()
    {
    }

    public virtual void OnUseEnded()
    {
    }

    public virtual void Dispose()
    {
        Cancel();

        Controller =
            null;

        Context =
            null;

        Data =
            null;

        IsInitialized =
            false;
    }
}
