using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public Transform blockPrefab;
    private int count = 1;
    private int reset = 200;
    public int colorType;
	
	private double [][][] locs = new double[][][]
	{
		// I
		new double[][]
		{
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {2.0, 0.0},
			new double[] {3.0, 0.0}
		},
		// O
		new double[][]
		{
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {0.0, 1.0},
			new double[] {1.0, 1.0}
		},
		// T
		new double[][]
		{
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {1.0, 1.0},
			new double[] {2.0, 0.0}
		},
		// S
		new double[][]
		{
			new double[] {0.0, 1.0},
			new double[] {1.0, 1.0},
			new double[] {1.0, 0.0},
			new double[] {2.0, 0.0}
		},
		// Z
		new double[][]
		{
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {1.0, 1.0},
			new double[] {2.0, 1.0}
		},
		// J
		new double[][]
		{
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {2.0, 0.0},
			new double[] {2.0, 1.0}
		},
		// L
		new double[][]
		{
			new double[] {0.0, 1.0},
			new double[] {0.0, 0.0},
			new double[] {1.0, 0.0},
			new double[] {2.0, 0.0}
		}
	};
	
	public Dictionary<Vector2, float> positions = new Dictionary<Vector2, float>();

	// Use this for initialization
    void Start () {
		
    }
	
	// Update is called once per frame
    void Update () {
        var x = Input.GetAxis("Horizontal") * Time.deltaTime * 150.0f;
        var z = Input.GetAxis("Vertical") * Time.deltaTime * 3.0f;

        //transform.Rotate(0, x, 0);
        //transform.Translate(0, 0, z);

        if (count % reset == 0)
        {
            colorType = Random.Range(0, 7);
            int xPos = Mathf.RoundToInt(Random.Range(-3.0f, 3.0f));
            for (int i = 0; i < 4; i++)
            {
                var blockTransform = Instantiate(blockPrefab) as Transform;
                blockTransform.position = new Vector3(xPos + (float) locs[colorType][i][0], 7 + (float) locs[colorType][i][1], 0);
            }
            // blockTransform.colorType = colorType;
        }

        count++;
    }
}
