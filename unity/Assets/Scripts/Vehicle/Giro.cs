using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Un script basico que hace girar el cepillo del recogedor de basura
public class Giro : MonoBehaviour
{
    //Velocidad angular
    public int  anguloporsegundo=3;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //La rotacion segun la velocidad angular
        transform.Rotate(new Vector3(anguloporsegundo, 0, 0) * Time.deltaTime);
    }
}
