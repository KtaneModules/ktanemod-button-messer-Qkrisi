using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Missions;
using RNG = UnityEngine.Random;

public class qkButtonMesser : MonoBehaviour {
    
    private Bomb FindSelfBomb()
    {
        var bombs = FindObjectsOfType<Bomb>();
        foreach(Bomb bomb in bombs)
        {
            foreach(BombComponent module in bomb.GetComponentsInChildren<BombComponent>(true))
            {
                if (module == GetComponent<BombComponent>()) return bomb;
            }
        }
        return null;
    }

    //From GeneralExtensions
    public static T Shuffle<T>(T list) where T : IList
    {
        if (list == null)
            throw new ArgumentNullException("list");
        for (int j = list.Count; j >= 1; j--)
        {
            int item = UnityEngine.Random.Range(0, j);
            if (item < j - 1)
            {
                var t = list[item];
                list[item] = list[j - 1];
                list[j - 1] = t;
            }
        }
        return list;
    }

    private Bomb SelfBomb;

    private GameObject _currentButton = null;

    private BombComponent[] selfModules = new BombComponent[] { };
    private List<Selectable> Selectables = new List<Selectable>();

    private Dictionary<Selectable, Func<bool>> Interactions = new Dictionary<Selectable, Func<bool>>();
    private Dictionary<Selectable, Action> InteractEnds = new Dictionary<Selectable, Action>();
    private List<Selectable> EnabledButtons = new List<Selectable>();
    private List<int> Indexes = new List<int>();

    [HideInInspector]
    public List<Selectable> UnlockedSelectables = new List<Selectable>();

    [HideInInspector]
    public List<Selectable> AvoidStrike = new List<Selectable>();       //For vanillas

    [HideInInspector]
    public List<Selectable> AvoidVanilla = new List<Selectable>();

    private readonly string[] Ignoreds = new[]
    {
        "Challenge & Contact",
        "Wire Sequence"
    };

    private readonly ModHandler[] SeparateHandle = new ModHandler[]
    {
        new ModHandler(typeof(SnippableWire), true),
        new ModHandler(typeof(VennSnippableWire), true),
        new ModHandler(typeof(WireSequenceWire))
    };

    private List<int> Availables
    {
        get
        {
            List<int> final = new List<int>();
            for(int i = 0;i<EnabledButtons.Count;i++)
            {
                if (!Indexes.Contains(i)) final.Add(i);
            }
            return final;
        }
    }

    private static int _counter;

    [HideInInspector]
    public int moduleID;

    private GameObject CurrentButton
    {
        get
        {
            return _currentButton;
        }
        set
        {
            if (_currentButton != null) Destroy(CurrentButton);
            _currentButton = Instantiate(value, FindFromRoot("CloneWrapper").transform);
            var selectable = _currentButton.GetComponent<Selectable>();
            selectable.Parent = null;
            selectable.Highlight = null;
            selectable.Children = new Selectable[] { };
            selectable.ChildRowLength = 0;
            selectable.enabled = false;
            _currentButton.transform.position = new Vector3(0,0,0);
            _currentButton.transform.rotation = value.transform.rotation;
            foreach (Transform t in _currentButton.transform) t.localPosition = new Vector3(0, t.localPosition.y, 0);
            _currentButton.transform.localPosition = new Vector3(0,0,0);
            _currentButton.transform.localRotation = new Quaternion(value.transform.localRotation.x, value.transform.localRotation.y + 180, value.transform.localRotation.z, value.transform.localRotation.w);
        }
    }

    [HideInInspector]
    public int _done = 0;

    private bool _forced = false;
    private bool _solve = false;

    private GameObject FindFromRoot(string _name)
    {
        return transform.Find("Objects").Find(_name).gameObject;
    }

    private bool CheckSolve()
    {
        if (Selectables.Count-EnabledButtons.Count == 0)
        {
            if (CurrentButton != null) Destroy(_currentButton);
            return true;
        }
        return false;
    }

    private void Logger(string msg)
    {
        Debug.LogFormat("[Button Messer #{0}] {1}", moduleID, msg);
    }

    private void SubmitButton(Selectable button)
    {
        if (_forced || EnabledButtons.Contains(button)) return;
        EnabledButtons.Add(button);
        var available = Availables;
        int ind = available[RNG.Range(0, available.Count)];
        Indexes.Add(ind);
        var messComponent = Selectables[ind].gameObject.AddComponent<Messed>();
        if (Interactions[Selectables[ind]] != null)
        {
            Selectables[ind].OnInteract = () =>
            {
                SubmitButton(Selectables[ind]);
                StrikePatch.striked = Selectables[ind];
                bool ret = Interactions[Selectables[ind]]();
                StrikePatch.striked = null;
                if (messComponent != null)
                {
                    Destroy(messComponent);
                    messComponent = null;
                }
                return ret;
            };
        }
        else
        {
            Selectables[ind].OnInteract = null;
            AvoidStrike.Add(Selectables[ind]);
            AvoidVanilla.Remove(Selectables[ind]);
        }
        UnlockedSelectables.Add(Selectables[ind]);
        CurrentButton = Selectables[ind].gameObject;
        if (CheckSolve()) GetComponent<KMBombModule>().HandlePass();
    }

    public void Start()
    {
        moduleID = ++_counter;
        SelfBomb = FindSelfBomb();
        var solveBTN = FindFromRoot("SolveButton");
        var handler = GetComponent<KMBombModule>();
        GetComponent<KMBombModule>().OnActivate += () =>
        {
            solveBTN.GetComponent<Selectable>().OnInteract += () =>
            {
                handler.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, solveBTN.transform);
                solveBTN.GetComponent<KMSelectable>().AddInteractionPunch(.5f);
                if (_solve) handler.HandlePass();
                return false;
            };
        };
        Debug.LogFormat("[Button Messer #{0}] {1}", moduleID, SelfBomb == null || moduleID != SelfBomb.GetComponentsInChildren<qkButtonMesser>(true).OrderByDescending(x => x.moduleID).ToList()[0].moduleID);
        if (SelfBomb==null || moduleID != SelfBomb.GetComponentsInChildren<qkButtonMesser>(true).OrderByDescending(x => x.moduleID).ToList()[0].moduleID)
        {
            _solve = true;
            return;
        }
        solveBTN.SetActive(false);
        selfModules = SelfBomb.GetComponentsInChildren<BombComponent>(true).Where(m => m.ComponentType != ComponentTypeEnum.Empty && m.ComponentType != ComponentTypeEnum.Timer && !Ignoreds.Contains(m.GetModuleDisplayName())).ToArray();
        foreach (BombComponent module in selfModules)
        {
            Selectables = Selectables.Concat(module.GetComponentsInChildren<Selectable>(true).Where(s => s.GetComponent<BombComponent>() == null && s.Parent != null && s.Parent.GetComponent<Bomb>() == null && s.GetComponent<ButtonMesser.messerOverride>() == null)).ToList();
        }
        foreach(ModHandler type in SeparateHandle)
        {
            Selectables = Selectables.Concat(SelfBomb.GetComponentsInChildren(type.t, type.Inactive).Select(x => x.GetComponent<Selectable>())).ToList();
        }
        Selectables = Selectables.Distinct().ToList();
        Selectables = Shuffle(Selectables);
        StartCoroutine(Starter());
    }

    private IEnumerator Starter()
    {
        yield return new WaitUntil(() => _done >= GetComponent<KMBombInfo>().GetModuleNames().Count);
        yield return null;
        Debug.LogFormat("Number of selectables: {0}, All selectables: {1}", Selectables.Count, SelfBomb.GetComponentsInChildren<SnippableWire>(true).Select(x => x.GetComponent<Selectable>()).ToList().Any(x => x==null));
        foreach(Selectable selectable in Selectables)
        {
            Func<bool> f = selectable.OnInteract == null ? null : new Func<bool>(selectable.OnInteract);
            if (f == null) AvoidVanilla.Add(selectable);
            Interactions.Add(selectable, f);
            InteractEnds.Add(selectable, selectable.OnInteractEnded);
            selectable.OnInteract = () => { SubmitButton(selectable); return false; };
        }
        if (CheckSolve())
        {
            _solve = true;
            FindFromRoot("SolveButton").SetActive(true);
            yield break;
        }
    }

    private void SetInteract(int index)
    {
		if(Interactions[Selectables[index]]==null) Selectables[index].OnInteract = null;
		else
		{
			Selectables[index].OnInteract = () => { return Interactions[Selectables[index]](); };
		}
        UnlockedSelectables.Add(Selectables[index]);
    }

    public void ResetAll()
    {
        _forced = true;
        for(int i = 0;i<Selectables.Count;i++)
        {
            SetInteract(i);
        }
    }

    public void Update()
    {
        if (GetComponent<KMBombInfo>().GetSolvableModuleIDs().All(module => module == "qkButtonMesser")) GetComponent<KMBombModule>().HandlePass();
    }

#pragma warning disable 414
    [HideInInspector]
    public string TwitchHelpMessage = "Use '!{0} press solve' if the solve button is present!";
#pragma warning restore 414
    public IEnumerator ProcessTwitchCommand(string command)
    {
        if(command.ToLowerInvariant()=="press solve")
        {
            yield return null;
            if(!_solve)
            {
                yield return "sendtochaterror The solve button isn't active!";
                yield break;
            }
            FindFromRoot("SolveButton").GetComponent<Selectable>().OnInteract();
        }
    }
}
