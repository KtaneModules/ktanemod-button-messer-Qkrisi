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
        foreach (Bomb bomb in bombs)
        {
            foreach (BombComponent module in bomb.GetComponentsInChildren<BombComponent>(true))
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

    private BombComponent[] selfModules = new BombComponent[] { };
    private List<Selectable> Selectables = new List<Selectable>();

    private Dictionary<Selectable, Transform> Cameras = new Dictionary<Selectable, Transform>();

    private Dictionary<Selectable, Func<bool>> Interactions = new Dictionary<Selectable, Func<bool>>();
    private Dictionary<Selectable, Action> InteractEnds = new Dictionary<Selectable, Action>();
    public List<Selectable> EnabledButtons = new List<Selectable>();
    private List<int> Indexes = new List<int>();

    private const int cameraLayer = 29;
    private Transform cam;
    private Dictionary<GameObject, int> ObjectLayers = new Dictionary<GameObject, int>();

    private Vector3 buttonPosition { get; set; }
    private Quaternion buttonRotation { get; set; }

    [HideInInspector]
    public List<Selectable> UnlockedSelectables = new List<Selectable>();

    [HideInInspector]
    public List<Selectable> AvoidStrike = new List<Selectable>();       //For vanillas

    [HideInInspector]
    public List<Selectable> AvoidVanilla = new List<Selectable>();

    private readonly string[] Ignoreds = new[]
    {
        "Button Messer",
        "Challenge & Contact",
        "Countdown",
        "Cruel Countdown",
        "Hold Ups",
        "Lightspeed",
        "Micro-Modules",
        "Only Connect",
        "Risky Wires",
        "Rubik's Clock",
        "The Modkit",
        "Ultimate Custom Night",
        "Word Search"
    };

    private readonly ModHandler[] SeparateHandle = new ModHandler[]
    {
        new ModHandler(typeof(SnippableWire)),
        new ModHandler(typeof(VennSnippableWire)),
        new ModHandler(typeof(WireSequenceWire))
    };

    public bool _enable = false;

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

    [HideInInspector]
    public int _done = 0;

    public bool _forced = false;
    private bool ButtonSolved = false;
    private bool _solve = false;
    private bool Pressable = false;

    [HideInInspector]
    public bool TwitchPlaysActive;

    private bool _Enabled => SelfBomb == null || moduleID !=
        SelfBomb.GetComponentsInChildren<qkButtonMesser>(true).OrderByDescending(x => x.moduleID).ToList()[0].moduleID;

    private void ChangeLayer(GameObject obj, bool reset = false)
    {
        if (!reset)
        {
            ObjectLayers.Add(obj, obj.layer);
            obj.layer = cameraLayer;
        }
        else
        {
            obj.layer = ObjectLayers[obj];
            ObjectLayers.Remove(obj);
        }
        foreach (Transform t in obj.transform) ChangeLayer(t.gameObject, reset);
    }

    private GameObject FindFromRoot(string _name)
    {
        return transform.Find("Objects").Find(_name).gameObject;
    }

    private bool CheckSolve()
    {
        return Selectables.Count - EnabledButtons.Count == 0;
    }

    private void Logger(string msg)
    {
        Debug.LogFormat("[Button Messer #{0}] {1}", moduleID, msg);
    }

    public void SubmitButton(Selectable button)
    {
        if (_forced || EnabledButtons.Contains(button)) return;
        EnabledButtons.Add(button);
        Logger($"Pressed button: {button.name}");
        var available = Availables;
        int ind = available[RNG.Range(0, available.Count)];
        Indexes.Add(ind);
        Logger($"Enabled button: {Selectables[ind].name}");
        Messed messComponent = null;
        messComponent = Selectables[ind].gameObject.AddComponent<Messed>();
        if (Interactions[Selectables[ind]] != null)
        {
            Selectables[ind].OnInteract = () =>
            {
                StrikePatch.striked = Selectables[ind];
                bool ret = Interactions[Selectables[ind]]();
                StrikePatch.striked = null;
                SubmitButton(Selectables[ind]);
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
        cam.transform.SetParent(Cameras[Selectables[ind]], false);
        StartCoroutine(RenderButton(Selectables[ind].gameObject));
        if (CheckSolve()) GetComponent<KMBombModule>().HandlePass();    
    }

    private IEnumerator RenderButton(GameObject button)
    {
        ChangeLayer(button);
        yield return new WaitForEndOfFrame();
        RenderTexture rt = new RenderTexture(256, 256, 24);
        Camera camObject = cam.GetComponent<Camera>();
        camObject.targetTexture = rt;
        Texture2D pic = new Texture2D(256, 256, TextureFormat.RGB24, false);
        camObject.Render();
        RenderTexture.active = rt;
        pic.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        pic.Apply();
        camObject.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        FindFromRoot("Display").GetComponent<Renderer>().material.mainTexture = pic;
        yield return new WaitForEndOfFrame();
        ChangeLayer(button, true);
    }

    public void Start()
    {
        moduleID = ++_counter;
        var AllIgnoreds = GetComponent<KMBossModule>().GetIgnoredModules("Button Messer", Ignoreds);
        SelfBomb = FindSelfBomb();
        var solveBTN = FindFromRoot("SolveButton");
        var handler = GetComponent<KMBombModule>();
        solveBTN.GetComponent<Selectable>().OnInteract += () =>
        {
            if (!Pressable) return false;
            StopCoroutine(Starter(solveBTN, AllIgnoreds));
            handler.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, solveBTN.transform);
            solveBTN.GetComponent<KMSelectable>().AddInteractionPunch(.5f);
            ButtonSolved = true;
            handler.HandlePass();
            return false;
        };
        GetComponent<KMBombModule>().OnActivate += () =>
        {
            Pressable = true;
            for (int i = 1; i <= 31; i++)
            {
                if (i == cameraLayer) continue;
                Physics.IgnoreLayerCollision(cameraLayer, i, Physics.GetIgnoreLayerCollision(solveBTN.layer, i));
            }
            cam = FindFromRoot("BCam").transform;
            cam.GetComponent<Camera>().cullingMask = 1 << cameraLayer;
            buttonPosition = FindFromRoot("PosState").transform.position;
            Destroy(FindFromRoot("PosState"));
            buttonRotation = FindFromRoot("SolveButton").transform.rotation;
        };
        StartCoroutine(Starter(solveBTN, AllIgnoreds));
    }

    private IEnumerator Starter(GameObject solveBTN, string[] AllIgnoreds)
    {
        yield return new WaitUntil(() => _done >= GetComponent<KMBombInfo>().GetModuleNames().Count);
        yield return null;
        solveBTN.SetActive(false);
        _enable = true;
        List<string> FinalIgnoreds = new List<string>();
        foreach(string module in AllIgnoreds)
        {
            if(String.IsNullOrEmpty(module))
            {
                if (TwitchPlaysActive) continue;
                break;
            }
            FinalIgnoreds.Add(module);
        }
        selfModules = SelfBomb.GetComponentsInChildren<BombComponent>(true).Where(m => m.ComponentType != ComponentTypeEnum.Empty && m.ComponentType != ComponentTypeEnum.Timer && !FinalIgnoreds.Contains(m.GetModuleDisplayName())).ToArray();
        foreach (BombComponent module in selfModules)
        {
            Transform ModuleCamera = SetupCamera(module);
            foreach (var selectable in module.GetComponentsInChildren<Selectable>(false).Where(s =>
                s.GetComponent<BombComponent>() == null && s.Parent != null && s.Parent.GetComponent<Bomb>() == null &&
                s.GetComponent<ButtonMesser.messerOverride>() == null))
            {
                Selectables.Add(selectable);
                Cameras.Add(selectable, ModuleCamera);
            }
            foreach (ModHandler type in SeparateHandle)
            {
                foreach (var selectable in module.GetComponentsInChildren(type.t, type.Inactive).Select(x => x.GetComponent<Selectable>()))
                {
                    Selectables.Add(selectable);
                    Cameras.Add(selectable, ModuleCamera);
                }
            }
        }
        Selectables = Selectables.Distinct().ToList();
        Selectables = Shuffle(Selectables);
        List<Selectable> remove = new List<Selectable>();
        foreach(Selectable selectable in Selectables)
        {
            try
            {
                Destroy(selectable.gameObject.AddComponent<Messed>());
            }
            catch(NullReferenceException)
            {
                remove.Add(selectable);
            }
        }
        foreach (Selectable r in remove) Selectables.Remove(r);
        Logger($"Number of messable buttons: {Selectables.Count}");
        foreach (Selectable selectable in Selectables)
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
            FindFromRoot("Display").SetActive(false);
            FindFromRoot("SolveButton").SetActive(true);
            yield break;
        }
    }

    private void SetInteract(int index)
    {
		if(Interactions[Selectables[index]]==null) Selectables[index].OnInteract = null;
		else
		{
			Selectables[index].OnInteract = () => Interactions[Selectables[index]]();
		}
        UnlockedSelectables.Add(Selectables[index]);
    }

    public void DestroyObject(Component obj)
    {
        Destroy(obj);
    }
    
    public void ResetAll()
    {
        if (ButtonSolved) return;
        _forced = true;
        FindFromRoot("Display").SetActive(false);
        for (int i = 0; i < Selectables.Count; i++) SetInteract(i);
    }

    public void Update()
    {
        if (GetComponent<KMBombInfo>().GetSolvableModuleIDs().All(module => module == "qkButtonMesser")) GetComponent<KMBombModule>().HandlePass();
    }
    
    private Transform SetupCamera(BombComponent module)        //Origin: TwitchPlays
    {
        Transform _cam = module.transform.Find("MesserCamera");
        if (_cam == null)
        {
            _cam = new GameObject().transform;
            _cam.name = "MesserCamera";
            _cam.SetParent(module.transform, false);
        }
        return _cam;
    }

    public void TwitchHandleForcedSolve()
    {
        var btn = FindFromRoot("SolveButton");
        if (Pressable && btn.activeInHierarchy) btn.GetComponent<Selectable>().OnInteract();
        else GetComponent<KMBombModule>().HandlePass();
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
            if (!Pressable)
            {
                yield return "sendtochaterror Button Messer didn't load yet!";
                yield break;
            }
            var button = FindFromRoot("SolveButton");
            if(!button.activeInHierarchy)
            {
                yield return "sendtochaterror The solve button isn't active!";
                yield break;
            }
            button.GetComponent<Selectable>().OnInteract();
        }
    }
}
