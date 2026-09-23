using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 한 번의 발사로 생성할 모든 투사체의 확정된 명세입니다.
/// </summary>
public sealed class ProjectileLaunchPlan
{
    private readonly ReadOnlyCollection<
        ProjectileLaunch> _launches;


    public static ProjectileLaunchPlan Empty
    {
        get;
    } = new(
        Array.Empty<
            ProjectileLaunch>());

    public IReadOnlyList<ProjectileLaunch>
        Launches =>
            _launches;

    public int Count =>
        _launches.Count;

    public bool IsEmpty =>
        Count == 0;

    public ProjectileLaunch this[int index] =>
        _launches[index];


    public ProjectileLaunchPlan(
        IReadOnlyList<
            ProjectileLaunch> launches)
    {
        if (launches == null ||
            launches.Count == 0)
        {
            _launches =
                Array.AsReadOnly(
                    Array.Empty<
                        ProjectileLaunch>());

            return;
        }

        ProjectileLaunch[] copy =
            new ProjectileLaunch[
                launches.Count];

        for (int i = 0;
             i < launches.Count;
             i++)
        {
            copy[i] =
                launches[i];
        }

        _launches =
            Array.AsReadOnly(
                copy);
    }


    public ProjectileLaunchPlan(
        params ProjectileLaunch[] launches)
        : this(
            (IReadOnlyList<
                ProjectileLaunch>)launches)
    {
    }
}
