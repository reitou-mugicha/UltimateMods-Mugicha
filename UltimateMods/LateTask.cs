// Source Code By SuperNewRoles

using System;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateMods
{
    class LateTask
    {
        public string name;
        public float timer;
        public Action action;
        public static List<LateTask> Tasks = new ();
        public bool run(float deltaTime)
        {
            timer -= deltaTime;
            if (timer <= 0)
            {
                action();
                return true;
            }
            return false;
        }
        public LateTask(Action action, float time, string name = "No Name Task")
        {
            this.action = action;
            this.timer = time;
            this.name = name;
            Tasks.Add(this);
            // Logger.info("New LateTask \"" + name + "\" is created");
        }
        public static void Update(float deltaTime)
        {
            var TasksToRemove = new List<LateTask>();
            Tasks.ForEach((task) =>
            {
                if (task.run(deltaTime))
                {
                    //Logger.info("LateTask \"" + task.name + "\" is finished");
                    TasksToRemove.Add(task);
                }
            });
            TasksToRemove.ForEach(task => Tasks.Remove(task));
        }
    }
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    class LateUpdate
    {
        public static void Postfix()
        {
            LateTask.Update(Time.deltaTime);
        }
    }
}