using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destruccion : MonoBehaviour
{
    //Esta funcion solo se activara cuando el colisionador detecte un trigger, este script esta integrado en cada basura
    void OnTriggerEnter(Collider other)
    {
        //Solo se activara si el trigger detectado tiene como tag "Barredora"
        if (other.gameObject.tag == "Barredora")
        {
            //Se destruye el objeto al cual este integrado este script
            Destroy(gameObject);
        }
        //Solo se activara si dicha basura se genera debajo de un recogedor de basura
        else if (other.gameObject.tag == "Recogedor" && Conteoglobal.elementos < Conteoglobal.basura_total)
        {
            //Destruye el objeto y reduce en uno la cantidad de basura que se genero antes de llegar al numero deseado
            Destroy(gameObject);
            Conteoglobal.elementos--;
        }
    }
}
