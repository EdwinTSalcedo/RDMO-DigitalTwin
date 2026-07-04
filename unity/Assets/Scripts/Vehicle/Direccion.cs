using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Direccion : MonoBehaviour
{
    //Se declaran las variables donde se localizaran los collider en forma de rueda
    [SerializeField] WheelCollider delantederecha;
    [SerializeField] WheelCollider delanteizquierda;
    [SerializeField] WheelCollider atrasderecha;
    [SerializeField] WheelCollider atrasizquierda;
    //Se declaran las variables para almacenar la posicion y rotacion de las ruedas
    [SerializeField] Transform delantederechamo;
    [SerializeField] Transform delanteizquierdamo;
    [SerializeField] Transform atrasderechamo;
    [SerializeField] Transform atrasizquierdamo;
    //Se declaran las variables de aceleracion y fuerza de frenado que tendra el vehiculo
    public float aceleracion=500f;
    public float fuerzaruptura=300f;
    //Se declara el angulo maximo que tendran las ruedas para girar de izquierda o derecha
    public float angulo=15f;
    //Se declara la variable donde se almacena la posicion del recogedor de basura en el inicio
    public Vector3 inicio;
    //Se declara las variables que varian hasta llegar a la aceleracion y fuerza que tiene de frenado
    public float aceleracionactual=0f;
    public float fuerzaactual=0f;
    //Se declara la variable del angulo actual hasta llegar al angulo 
    public float anguloactual=0f;
    Rigidbody rb;
    float rotationY;
    private void Start() {
        //Se almacena la ubicacion inicial del recogedor de basura
        inicio=transform.position;
    }
    private void Update() {
        //Se hacen dos condicionales, las cuales solo se activan si el recogedor de basura llega a un angulo de inclinacion determinado
        if(transform.localEulerAngles.z>260 && transform.localEulerAngles.z<328)
        {
            //Se reajusta el angulo del recogedor a uno estable y se teletransporta al mismo a su posicion inicial 
            transform.localRotation = Quaternion.Euler(0, 0, 0);
            transform.position = inicio;
            
        }
        else if(transform.localEulerAngles.z>32 && transform.localEulerAngles.z<100)
        {
            //Se reajusta el angulo del recogedor a uno estable y se teletransporta al mismo a su posicion inicial 
            transform.localRotation = Quaternion.Euler(0, 0, 0);
            transform.position = inicio;
            
        }
        float horizontalInput = Input.GetAxis("Horizontal");
        bool canRotate = horizontalInput != 0;
        if (!canRotate)
        {
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, rotationY, transform.rotation.eulerAngles.z);
        }
        else
        {
            rotationY=  transform.rotation.eulerAngles.y;
        }
    }
    private void FixedUpdate() {
        //La variable almacena un valor que cambia segun el boton arriba/abajo o w/s considerados Vertical
        aceleracionactual=aceleracion*Input.GetAxis("Vertical");
        //Al mantener presionado el boton "q" se pone la fuerza de freno al maximo, frenando el mismo
        if(Input.GetKey("q"))
            fuerzaactual=fuerzaruptura;
        else
            fuerzaactual=0f;
        //Se ajusta la aceleracion de las colisiones de ruedas segun la aceleracion actual
        delantederecha.motorTorque=aceleracionactual;
        delanteizquierda.motorTorque=aceleracionactual;
        //Se ajusta la fuerza del frenado
        delantederecha.brakeTorque=fuerzaactual;
        delanteizquierda.brakeTorque=fuerzaactual;
        atrasderecha.brakeTorque=fuerzaactual;
        atrasizquierda.brakeTorque=fuerzaactual;
        //Se cambia la variable segun se mantenga presionado los botones izquierda/derecha o a/d 
        anguloactual=angulo*Input.GetAxis("Horizontal");
        //Se ajusta las variables del angulo de cada rueda
        delanteizquierda.steerAngle=anguloactual;
        delantederecha.steerAngle=anguloactual;

        Actualizacionruedas(delanteizquierda,delanteizquierdamo);
        Actualizacionruedas(delantederecha,delantederechamo);
        Actualizacionruedas(atrasizquierda,atrasizquierdamo);
        Actualizacionruedas(atrasderecha,atrasderechamo);

    }
    void Actualizacionruedas(WheelCollider col,Transform trans){
        //Se ajusta la posicion y rotacion del modelado de las ruedas respeto al angulo de la colision de las ruedas
        Vector3 posicion;
        Quaternion rotacion;
        col.GetWorldPose(out posicion, out rotacion);

        trans.position=posicion;
        trans.rotation=rotacion;
    }

 }

