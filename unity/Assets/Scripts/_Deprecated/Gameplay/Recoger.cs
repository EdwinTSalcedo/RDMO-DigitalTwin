using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Recoger : MonoBehaviour
{
    //Se activara cada vez que algo ingrese al Detector
    void OnTriggerEnter(Collider other)
    {
        //Si la basura, tiene o es catalogada con el nombre definido, se contara en el mismo y tambien el numero total de basura recogida
        if (other.gameObject.tag == "Vidrio")
        {
            Conteoglobal.conteovidrio++;
            Conteoglobal.conteototal++;
        }
        if (other.gameObject.tag == "Metal")
        {
            Conteoglobal.conteometal++;
            Conteoglobal.conteototal++;
        }
        if (other.gameObject.tag == "Papel")
        {
            Conteoglobal.conteopapel++;
            Conteoglobal.conteototal++;
        }
    }
    
}
