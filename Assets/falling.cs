using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class falling : MonoBehaviour {
	public Vector3 speed = new Vector3(0, -2, 0);
	private int count = 0;

	private Vector3 movement;
	private Vector3 stop;
	private Rigidbody rigidbodyComponent;
	int colorType = 0;

	private double[][] colors = new double[][]
	{
		///*
		// cyan
		new double[] {0.5, 1.0, 1.0},
		// yellow
		new double[] {1/6.0, 1.0, 1.0},
		// purple
		new double[] {5/6.0, 1.0, 0.502},
		// green
		new double[] {1/3.0, 1.0, 1.0},
		// red
		new double[] {0, 1.0, 1.0},
		// blue
		new double[] {2/3.0, 1.0, 1.0},
		// orange
		new double[] {33/360.0, 1.0, 1.0}
		//*
	};

	// Use this for initialization
	void Start ()
	{
        transform.position = new Vector3(0.0F,
                                         0.0F,
                                         10.0F);
		
		Renderer rend = GetComponent<Renderer>();

		rend.material.shader = Shader.Find("_Color");
		double[] color = colors[0];
		rend.material.SetColor("_Color", Color.HSVToRGB((float) color[0], (float) color[1], (float) color[2]));

		rend.material.shader = Shader.Find("Specular");
		rend.material.SetColor("_SpecColor", Color.HSVToRGB((float) color[0], (float) color[1], (float) color[2]));
		//Debug.Log(colorType);
	}
	
	// Update is called once per frame
	void Update () {
		if (count % 50 == 0)
            transform.Translate(0, 0, -1, Space.World);
        //transform.Translate(0, speed.y, 0);
		/*
		var x = Input.GetAxis("VerticalRotate") * Time.deltaTime * 150.0f;
		var y = Input.GetAxis("HorizontalRotate") * Time.deltaTime * 150.0f;
		var z = Input.GetAxis("DepthRotate") * Time.deltaTime * 150.0f;
		*/
        /*
		if (Input.GetKeyUp("a"))
			transform.Translate(-1, 0, 0);
		if (Input.GetKeyUp("d"))
			transform.Translate(1, 0, 0);
		if (Input.GetKeyUp("w"))
			transform.Translate(0, 0, 1);
		if (Input.GetKeyUp("s"))
			transform.Translate(0, 0, -1);
		if (Input.GetKeyUp("2"))
			transform.Rotate(30, 0, 0);
		if (Input.GetKeyUp("x"))
			transform.Rotate(-30, 0, 0);
		if (Input.GetKeyUp("q"))
			transform.Rotate(0, 30, 0);
		if (Input.GetKeyUp("e"))
			transform.Rotate(0, -30, 0);
		if (Input.GetKeyUp("z"))
			transform.Rotate(0, 0, -30);
		if (Input.GetKeyUp("c"))
			transform.Rotate(0, 0, 30);
        //*/
	}

    /*
	void FixedUpdate()
	{
		if (rigidbodyComponent == null)
			rigidbodyComponent = GetComponent<Rigidbody>();

		//rigidbodyComponent.velocity = movement;
		
		//Debug.Log(rigidbodyComponent.velocity);
		
		count++;
	}

	void OnCollisionEnter(Collision collision)
	{
		//Debug.Log("hello");
		speed = new Vector3(0, 0, 0);
	}
    //*/
}
