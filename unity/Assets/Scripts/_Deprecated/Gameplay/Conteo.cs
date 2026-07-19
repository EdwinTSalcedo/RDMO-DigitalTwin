using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Se agrego TMPro para tener mas control sobre el estilo de la letra que aparecera en UI
using TMPro;
 
public class Conteo : MonoBehaviour
{
    //Se declaran variables dentro de un SerializeField, esto permite visualizarse y modificar variables private desde el editor, y evita su modificacion usando codigo.
    [SerializeField]
    private TextMeshProUGUI vidrio;
    [SerializeField]
    private TextMeshProUGUI metal;
    [SerializeField]
    private TextMeshProUGUI papel;
    //Se realizar estas operaciones al iniciar el programa
    void Start() {
        //Se modifica el texto de la UI que muestra la basura recogida, el cual aumenta su valor y se convierte en texto para la visualizacion inicial
        vidrio.text ="Vidrio: "+Conteoglobal.conteovidrio.ToString(); 
        metal.text = "Metal: "+Conteoglobal.conteometal.ToString(); 
        papel.text = "Papel: "+Conteoglobal.conteopapel.ToString(); 
    }
    void Update() {
        //Se modifica el texto de la UI que muestra la basura recogida, el cual aumenta su valor y se convierte en texto para la visualizacion en cada actualizacion de frame
        vidrio.text ="Vidrio: "+Conteoglobal.conteovidrio.ToString(); 
        metal.text = "Metal: "+Conteoglobal.conteometal.ToString(); 
        papel.text = "Papel: "+Conteoglobal.conteopapel.ToString(); 
    }
}
