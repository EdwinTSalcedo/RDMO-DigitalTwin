using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Generador : MonoBehaviour
{
    //Se crea una lista que almacenara la basura, el tamaño de la misma se puede modificar desde el editor y se le agrega la basura a gusto
    public List<GameObject> taco = new List<GameObject>();
    //Se crea la variable que almacenara el tamaño de la lista creada
    public int conteo;
    //Se crea la variable que designara el numero de area donde se generara la basura
    public int seccion;
    //Se crea la variable que almacena el script que se encarga del conteo de la basura generada
    public Recoger conteorecolector;
    //Se realizan las operaciones en esta funcion, antes de iniciar la simulacion
    private void Awake() 
    {
        //Se almacena el tamaño de la lista de basura
        conteo=taco.Count;
    }
    //Se realiza las operaciones constantemente segun un ciclo determinado, sin depender de los frames
    private void FixedUpdate() {
        //Se realiza la operacion solo si la basura aun no llega al numero total requerido
        if(Conteoglobal.elementos<Conteoglobal.basura_total)
        {
            //Se genera numero random 
            seccion=Random.Range(0,4);
            //Se realiza una operacion segun el numero de area que se creo
            switch(seccion)
            {
                case 0:
                    //Se crea la basura segun un numero random que se limita segun el tamaño de la lista de basura, en un punto random del area designada
                    Instantiate(taco[Random.Range(0,conteo)], new Vector3(Random.Range(-14f,-15f), 0.4f,Random.Range(-51.5f,-14.5f)), Quaternion.Euler(45, 45,45));
                    Conteoglobal.elementos++;
                    break;
                case 1:
                    //Se crea la basura segun un numero random que se limita segun el tamaño de la lista de basura, en un punto random del area designada
                    Instantiate(taco[Random.Range(0,conteo)], new Vector3(Random.Range(-14.5f,-94.5f), 0.4f,Random.Range(-14f,-15f)), Quaternion.Euler(45, 45,45));
                    Conteoglobal.elementos++;
                    break;
                case 2:
                    //Se crea la basura segun un numero random que se limita segun el tamaño de la lista de basura, en un punto random del area designada
                    Instantiate(taco[Random.Range(0,conteo)], new Vector3(Random.Range(-94f,-95f), 0.4f,Random.Range(-14.5f,-51.5f)), Quaternion.Euler(45, 45,45));
                    Conteoglobal.elementos++;
                    break;
                case 3:
                    //Se crea la basura segun un numero random que se limita segun el tamaño de la lista de basura, en un punto random del area designada
                    Instantiate(taco[Random.Range(0,conteo)], new Vector3(Random.Range(-94.5f,-14.5f), 0.4f,Random.Range(-51f,-52f)), Quaternion.Euler(45, 45,45));
                    Conteoglobal.elementos++;
                    break;
                
            }
            
        }
        //Esta condicion solo se activa si el recogedor de basura recoge toda la basura que se ha generado
        if(Conteoglobal.elementos==Conteoglobal.conteototal)
        {
            //Se vuelve los valores de la basura en 0, provocando que se vuelva a generar la basura
            Conteoglobal.conteototal=0;
            Conteoglobal.elementos=0;
        }
    }
}