using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Sin necesidad de integrar este script a algun gameobject, este puede ser referenciado por cualquier codigo sin necesidad de integrarlo al mismo.
public static class Conteoglobal
{
    //Se almacena el valor del tipo de basura que se recoge segun cada variable
    public static int conteovidrio;
    public static int conteometal;
    public static int conteopapel;
    //Se cuenta el total de la basura que se recogio
    public static int conteototal;
    //Se cuenta la basura que se creo
    public static int elementos;
    //Se almacena la basura que se va a generar en cada ciclo
    public static int basura_total=500;
}
