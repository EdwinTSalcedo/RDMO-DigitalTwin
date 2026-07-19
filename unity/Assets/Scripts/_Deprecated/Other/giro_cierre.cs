using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Script que se encarga de recoger o desplegar la boca del recogedor de basura, al no estar despleago, no puede recoger basura
public class giro_cierre : MonoBehaviour
{
    public bool arriba;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        //Esta condicional solo se activa si la boca esta elevado
        if(arriba)
        {
            //Si la boca del basurero esta cerca de llegar al angulo deseado, se rota el mismo al angulo deseado de manera forzada, evitando decimales extra
            if(transform.localEulerAngles.x<=313 || transform.localEulerAngles.x>=317)
            {
                transform.Rotate(new Vector3(-45, 0, 0) * Time.deltaTime);
            }
        }
        //Esta condicional solo se activa si la boca esta abajo
        else if(!arriba)
        {
            //Si la boca del basurero esta cerca de llegar al angulo deseado, se rota el mismo al angulo deseado de manera forzada, evitando decimales extra
            if(transform.localEulerAngles.x>=300 )
            {
                transform.Rotate(new Vector3(45, 0, 0) * Time.deltaTime);
            }
        }
        //Segun se presione el boton, se cambiara el estado que tendria la boca del recogedor de basura, y segun esto el giro que dara el recogedor.
        if (Input.GetKeyDown(KeyCode.Space) )
        {
            if(!arriba)
            {
                arriba=true;
            }
            else if(arriba)
            {
                arriba=false;
            }
        }

    }
}
