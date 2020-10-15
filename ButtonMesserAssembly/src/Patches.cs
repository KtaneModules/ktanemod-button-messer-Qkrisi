using System.Linq;
using log4net;
using HarmonyLib;

public static class Patcher
{
    public static qkButtonMesser GetMeser(BombComponent __instance)
    {
        if (__instance == null) return null;
        var messers = __instance.transform.parent.GetComponentsInChildren<qkButtonMesser>(true);
        if (messers.Length > 0) return messers.OrderByDescending(x => x.moduleID).ToArray()[0];
        return null;
    }

    public static Selectable GetParent(Selectable parented)
    {
        while (parented.Parent != null && parented.Parent.GetComponent<BombComponent>() == null) parented = parented.Parent;
        return parented;
    }

    public static void Patch()
    {
        new Harmony("qkrisi.buttonmesser").PatchAll();
    }
}

[HarmonyPatch(typeof(BombComponent), "HandlePass", MethodType.Normal)]
public class SolvePatch
{
    public static void Prefix(BombComponent __instance)
    {
        if (__instance.GetComponent<qkButtonMesser>() != null) __instance.GetComponent<qkButtonMesser>().ResetAll();
    }
}

[HarmonyPatch(typeof(BombComponent), "HandleStrike", MethodType.Normal)]
public class StrikePatch
{
    public static Selectable striked = null;

    public static bool Prefix(BombComponent __instance)
    {
        return striked==null || striked.GetComponent<Messed>() == null;
    }
}

[HarmonyPatch(typeof(BombComponent), "Activate", MethodType.Normal)]
public class ActivatePatch
{
    public static void Prefix(BombComponent __instance)
    {
        var messer = Patcher.GetMeser(__instance);
        if (messer != null) messer._done += 1;
    }
}

[HarmonyPatch(typeof(Selectable), "HandleInteract", MethodType.Normal)]
public class PressPatch
{
    public static bool Prefix(Selectable __instance, ILog ___logger, out qkButtonMesser __state, ref bool __result)
    {
        Selectable parented = Patcher.GetParent(__instance);
        var messer = Patcher.GetMeser((parented.Parent == null ? parented : parented.Parent).GetComponent<BombComponent>());
        if (messer != null && messer._enable)
        {    
            if (messer._forced)
            {
                if(__instance.GetComponent<Messed>()!=null) messer.DestroyObject(__instance.GetComponent<Messed>());
                __state = messer;
                return true;
            }
            if(messer.AvoidStrike.Contains(__instance))
            {
                messer.SubmitButton(__instance);
                StrikePatch.striked = __instance;
                __state = messer;
                return true;
            }
            if (messer.AvoidVanilla.Contains(__instance))
            {
                __state = null;
                bool flag = true;
                ___logger.DebugFormat("OnInteract: {0}", __instance.name);
                BombComponent component = __instance.GetComponent<BombComponent>();
                if (component != null)
                {
                    component.Focused();
                    if (__instance.OnFocus != null) __instance.OnFocus();
                }
                if (__instance.OnInteract != null) flag &= __instance.OnInteract();
                __result = flag;
                return false;
            }
        }
        __state = null;
        return true;
    }

    public static void Postfix(Selectable __instance, qkButtonMesser __state)
    {
        if (__state != null)
        {
            StrikePatch.striked = null;
            __state.AvoidStrike.Remove(__instance);
        }
    }
}

[HarmonyPatch(typeof(Selectable), "OnInteractEnded", MethodType.Normal)]
public class EndPatch
{
    public static bool Prefix(Selectable __instance)
    {
        Selectable parented = Patcher.GetParent(__instance);
        var messer = Patcher.GetMeser((parented.Parent == null ? parented : parented.Parent).GetComponent<BombComponent>());
        return messer == null || messer.UnlockedSelectables.Contains(__instance);
    }
}