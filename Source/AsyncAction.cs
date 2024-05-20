namespace AICore;

public abstract class AsyncAction
{
    public virtual bool IsValid => true;

    public abstract void Invoke();

    public abstract void ReturnToPool();

    public virtual void ExceptionThrown(Exception ex) { }
}
