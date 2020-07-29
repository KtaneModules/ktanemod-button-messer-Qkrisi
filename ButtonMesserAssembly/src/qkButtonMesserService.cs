using UnityEngine;

public class qkButtonMesserService : MonoBehaviour {
	void Awake()
    {
        Patcher.Patch();
    }
}
