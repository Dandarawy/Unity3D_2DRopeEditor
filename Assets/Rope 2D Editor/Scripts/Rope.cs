using UnityEngine;
using System.Collections.Generic;

public class Rope : MonoBehaviour {
    public List<Vector3> nodes = new List<Vector3>();
    public SpriteRenderer chainPart;
    [Range(-0.5f,0.5f)]
    public float overlapFactor;
    // Use this for initialization
    void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
