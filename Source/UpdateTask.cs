using UnityEngine;
using Verse;

namespace AICore;

// for scheduling a recurring action
// - updateIntervalFunc is a function that returns the time in ticks between invoking the action
// - action is executed at each scheduled interval
// - startImmediately to indicate the action will be executed as soon as the task starts
//
public struct UpdateTaskTick(Func<int> updateIntervalFunc, Action action, bool startImmediately)
{
    public int updateTickCounter = startImmediately
        ? 0
        : Rand.Range(updateIntervalFunc() / 2, updateIntervalFunc());
    public Func<int> updateIntervalFunc = updateIntervalFunc;
    public Action action = action;
}

// for scheduling a recurring action
// - updateIntervalFunc is a function that returns the time in ticks between invoking the action
// - action is executed at each scheduled interval
// - startImmediately to indicate the action will be executed as soon as the task starts
//
public struct UpdateTaskTime(Func<float> updateIntervalFunc, Action action, bool startImmediately)
{
    public float nextUpdateTime = startImmediately
        ? Time.realtimeSinceStartup
        : Time.realtimeSinceStartup + updateIntervalFunc();
    public Func<float> updateIntervalFunc = updateIntervalFunc;
    public Action action = action;
}
