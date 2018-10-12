
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using System.Runtime.InteropServices;
using POSstation.Protocolos;
using System.Net.Sockets;
using Modbus.Device;
using System.Net;
using System.Net.NetworkInformation;




//using gasolutions.Factory;
namespace POSstation.Protocolos
{
    public class FillPos_IMW: iProtocolo
    {
        #region EventosDeProtocolo

        public bool AplicaServicioWindows = true;
        public bool AplicaServicioTramas = true;
        public event iProtocolo.CambioMangueraEnVentaGerenciadaEventHandler CambioMangueraEnVentaGerenciada;//

        public event iProtocolo.CaraEnReposoEventHandler CaraEnReposo;//--

        public event iProtocolo.VentaFinalizadaEventHandler VentaFinalizada;//--

        public event iProtocolo.LecturaTurnoCerradoEventHandler LecturaTurnoCerrado;//--

        public event iProtocolo.LecturaTurnoAbiertoEventHandler LecturaTurnoAbierto;//--

        public event iProtocolo.LecturaInicialVentaEventHandler LecturaInicialVenta;//--

        public event iProtocolo.VentaParcialEventHandler VentaParcial;//

        public event iProtocolo.CambioPrecioFallidoEventHandler CambioPrecioFallido;//

        public event iProtocolo.CancelarProcesarTurnoEventHandler CancelarProcesarTurno;//--

        public event iProtocolo.ExcepcionOcurridaEventHandler ExcepcionOcurrida;//--

        public event iProtocolo.VentaInterrumpidaEnCeroEventHandler VentaInterrumpidaEnCero;//

        public event iProtocolo.AutorizacionRequeridaEventHandler AutorizacionRequerida;//--

        public event iProtocolo.IniciarCambioTarjetaEventHandler IniciarCambioTarjeta;//

        public event iProtocolo.LecturasCambioTarjetaEventHandler LecturasCambioTarjeta;//

        public event iProtocolo.NotificarCambioPrecioMangueraEventHandler NotificarCambioPrecioManguera;//

        #endregion


        #region DECLARACION DE VARIABLES Y DEFINICIONES

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;        //Diccionario donde se almacenan las Caras y sus propiedades
        ComandoSurtidor ComandoCaras;
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        //SharedEventsFuelStation.CMensaje Eventos;                                 //Controla la comunicacion entre las aplicaciones por medio de eventos
        //System.Timers.Timer PollingTimer = new System.Timers.Timer(20); //Definicion del TIMER DE ENCUESTA
        //EGV:Instancia Arreglo de lecturas para reportar reactivación de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       
        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int BytesEsperados_Extended;
        int BytesRecibido;              // DCF 13-02-2012
        int BytesRecibido_Extended;
        int Bytes_leidos;
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no
        bool InconsistenciaDatosRx;     //Bandera que indica si hay inconsistencia en la trama recibida del surtidor: CRC, Cara que responde, etc
        bool ErrorComunicacion;         //Bandera que indica si hubo error en la comunicación: Trama recibida con longitud 0 o incompleta

        //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        string Archivo;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWRegistro;

        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;
        AsyncCallback callBack = new AsyncCallback(CallBackMethod);
        //DCF 16/04/2012
        //string ArchivoTramas2;
        //StreamWriter SWTramas2;

        bool CondicionCiclo = true;

        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        byte CaraID;//DCF Alias 

        ushort Register; //Registo para lectura o escritura Modbus TCP/IP
        ushort Value_Registo; //Valor a escribir en el registro Modbus
        ushort Numeros_Registro; //Cantidad de registros a Leer
        bool Tipo; //   bool Tipo; //True Escitura - False lectura

       
        //Variables para PLC
        ushort[] values; // valores que entrega el PLC
        int data1 = 0;
        int data2 = 0;
       
        string hexValue1 = "";
        string hexValue2 = "";

        string Valor = "";

        public enum ComandoSurtidor
        {

            Heartbeat1, //Pulsos cada 5 seg
            Heartbeat0,
            Autorizar,
            System_Alarma,
            Power_Up,

            Status_,
            Status_ESD,
            Status_Alarma,
            Status_Fulling,
            Status_Ready,

            Out_Pos_HB, //Repuesta Heartbeat
            Post_Volumen_TGT,
            Mass_Fill,
            Mass_Daily,
            Mass_Last_Daily,
            Mass_Total,
            Volumen_Fill,
            Volumen_Daily,
            Volumen_Last_Daily,
            Volumen_Total,
            Post_Pressure,
            Gas_Density,
            Post_Pressure_TGT,





            Estado = 0x03,
         
            EnviarDatos = 0x20,
            Detener = 0x30,
            TotalDespacho = 0x40,
            Totales = 0x50,
            ParcialDespacho = 0x60,
            DetenerTodos = 0xFC,
            //Trama para transmision de datos a la Cara, enviados despues del comando 0x02 (EnviarDatos)            
            CambiarPrecio,
            PredeterminarVentaDinero,
            PredeterminarVentaVolumen,
            EstadoExtendido
        }   //Define los posibles COMANDOS que se envian al Surtidor

        //Declaro el Delegado para la Funcion que me maneja El Lanzamiento del Evento
        public delegate string AsyncMethodCaller(string[] args);

        //Definicion del Hilo
        public Thread HiloCicloCaras;


        #endregion

        #region METODOS PRINCIPALES


        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        Int16 Puerto;

        TcpClient ClienteFillPos_IMW;
        NetworkStream Stream;

        //byte[] TramaRxTemporal = new byte[250];
        int BytesRecibidos = 0;

        int out_pos_hb = 0;
        int Contador_Visula0 = 0;
        int Contador_Visula1 = 0;
        int Contador_Visula2 = 0; 
        int Contador_Visula3 = 0; 
        
        
        public FillPos_IMW(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {                                
                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-FillPos_IMW-Sucesos(" + DireccionIP + ").txt";
                SWRegistro = File.AppendText(Archivo);



                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Convert.ToInt16(Puerto);
               
                AplicaServicioWindows = true;
                TramaEco = Eco;
                

                if (EsTCPIP)
                {
                    try
                    {
                        bool Conectado = false;
                        int conte = 10;
                        int reint = 0; 
                        while (!Conectado)
                        {
                            try
                            {

                                if (ClienteFillPos_IMW != null)
                                {  //********************************************
                                    //DESCONEXION 

                                    Stream.Close();
                                    Stream.Dispose();
                                    Stream = null;
                                    ClienteFillPos_IMW.Close();
                                    ClienteFillPos_IMW = null;

                                    //********************************************
                                }
                                else
                                {

                                    ////Crea y abre la conexión con el Servidor
                                    ClienteFillPos_IMW = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                                    Stream = ClienteFillPos_IMW.GetStream();
                                    Conectado = true;

                                    if (ClienteFillPos_IMW != null)
                                    {
                                     string MensajeExcepcion = "|0|Conexión con el Server: " + DireccionIP + ": " + Puerto ; 
                                    SWRegistro.WriteLine(DateTime.Now + "" + MensajeExcepcion);
                                    SWRegistro.Flush();
                                    }
                                    else
                                    {
                                        string MensajeExcepcion = "|0|No conexión con el ServerXX: " + DireccionIP + ": " + Puerto;
                                        SWRegistro.WriteLine(DateTime.Now + "" + MensajeExcepcion);
                                        SWRegistro.Flush();

                                    }
                                }

                            }
                            catch 
                            {
                                Conectado = false;

                                Thread.Sleep(500);//Tiempo de espera


                                if (conte > 10 )
                                {
                                    string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + " -Reintento: " + reint;
                                    SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                                    SWRegistro.Flush();
                                    reint++;

                                    conte = 0; 

                                }
                                conte++; 





                            
                            }

                        }

                      //  ModbusIpMaster master = ModbusIpMaster.CreateIp(ClienteFillPos_IMW);

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }

             
                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-FillPos_IMW-Tramas.(" + DireccionIP + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 22.01.2015-1511"); //FillPos_IMW
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 19.05.2015-1130"); //FillPos_IMW
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 27.05.2015- 1720"); //FillPos_IMW
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 05.06.2015- 1548"); //
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 08.04.2016- 1205"); // 04-04-2016 -1700
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 19.05.2016- 1054"); // Logueo apertura de turno 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 19.05.2016- 1612");//DCF verificar coneccion //DCF 19_05_2016 1612
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 24.05.2016- 1157");//Turno para las caras pares e impares 
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo FillPos_IMW_TCPIP. Modificado 26.05.2016- 1800");// DCF 25-05-2016
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Numero de caras: " + EstructuraRedSurtidor.Count);
                SWRegistro.Flush();


                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                   

                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " IP: " + DireccionIP + " - Port: " + Puerto);
                }
                SWRegistro.Flush();



                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Antes del SubProceso:");
                SWRegistro.Flush();

               ThreadPool.QueueUserWorkItem(CicloCara,""); // NO funcina en Pero PC  04-04-2016 -1700

              

               

                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Despues del SubProceso:");
                SWRegistro.Flush();




            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase Gilbarco: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public byte ConvertirCaraBD(byte caraBD) 
        {
            byte CaraSurtidor = 0;
            try
            {
                foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                {
                    if (ORedCaras.CaraBD == caraBD)
                    {
                        CaraSurtidor = ORedCaras.Cara;
                        break;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en funcion ConvertirCaraBD";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();

            }
            return CaraSurtidor;
        }


        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        public void CicloCara(object c)
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;



                SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "CondicionCiclo" +  CondicionCiclo );
                SWRegistro.Flush();

                foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                {


                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "EstructuraRedSurtido: " + EstructuraRedSurtidor.Values);
                    SWRegistro.Flush();


                
                    CaraEncuestada = ORedCaras.Cara;//Cara Asignado 

                    //envio de comando peticion de Totalizador inicial
                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "CaraEncuestada: " + CaraEncuestada);
                    SWRegistro.Flush();

                    CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "CaraID: " + CaraID);
                    SWRegistro.Flush();



                    CaraID = CaraEncuestada ;


                   // CaraEncuestada = 12;

                    //envio de comando peticion de Totalizador inicial
                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "CaraEncuestada: " + CaraEncuestada);
                    SWRegistro.Flush();
                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "CaraID: " + CaraID);
                    SWRegistro.Flush();


                    foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                    {
                        
                        SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Antes de toma lectura  *********  " + CaraID);
                        SWRegistro.Flush();
                        
                        //foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        //{
                            TomarLecturas();

                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Despues de toma lectura  *********  " + CaraID);
                        SWRegistro.Flush();

                           

                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura;

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inicio|Grado: 0 " + " - Precio: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].PrecioNivel1 + " -Lectura Inicial = " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta +
                                " IP: " + DireccionIP + " - Port: " + Puerto);
                            SWRegistro.Flush();
                        //}
                    }                    

                 }
             



                //Ciclo Infinito
                while (CondicionCiclo)
                {



                    VerifySizeFile();

                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {

                       // SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + "EstructuraRedSurtidor.Values ** : " + EstructuraRedSurtidor.Values );  
                        //Si la cara está activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {
                            CaraEncuestada = ORedCaras.Cara;//Cara Asignado 


                            //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + " CaraEncuestada : " + CaraEncuestada);                      
                           
                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                            //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + " CaraID : " + CaraID);

                           CaraID = CaraEncuestada;

                            //CaraEncuestada = 12;

                            //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + " CaraEncuestada : " + CaraEncuestada);

                            //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio|Grado: 0 " + " CaraID : " + CaraID);


                            //envio de Bit de comunicacion pulsos


                            ComandoCaras = ComandoSurtidor.Heartbeat1;
                            if (ProcesoEnvioComando(ComandoSurtidor.Heartbeat1, true))
                            {
                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria

                                ComandoCaras = ComandoSurtidor.Status_;
                                if (ProcesoEnvioComando(ComandoSurtidor.Status_, true))
                                    TomarAccion();

                                ComandoCaras = ComandoSurtidor.Heartbeat0;
                                ProcesoEnvioComando(ComandoSurtidor.Heartbeat0, true);
                            }


                            else
                            {
                                //////Crea y abre la conexión con el Servidor
                                //ClienteFillPos_IMW = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                                //Stream = ClienteFillPos_IMW.GetStream();



                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| Reconexion ");
                                SWRegistro.Flush();

                                Reconexion(); 

                            }

                        }

                        Thread.Sleep(200);

                    }
                }
            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        public bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar, bool PrecioNivel1)
        {
            try
            {

                //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 3 " + CaraID);
                //SWRegistro.Flush();
                //Puerto utilizado por el autorizador para imprimir mensajes de error
                string PuertoAImprimir;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 3;//Antes 5

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa el vector de control de fallo de comunicación
                InconsistenciaDatosRx = false;
                ErrorComunicacion = false;

                //Arma la trama de Transmision              
               
                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 4 " + CaraID);
                    //SWRegistro.Flush();

                    ArmarTramaTx(ComandoaEnviar, PrecioNivel1);

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 5 " + CaraID);
                    //SWRegistro.Flush();

                    if (EsTCPIP)
                    { 
                        EnviarComando_TCPIP();

                  
                    //leer el registro dependiendo de la consulta:
                        RecibirInformacion_TCPIP();

                    }
                    Reintentos++;
                         
                } while (((InconsistenciaDatosRx == true) || (ErrorComunicacion == true)) && (Reintentos < MaximoReintento));

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (InconsistenciaDatosRx == true || ErrorComunicacion == true)
                {
                    //EGV:Si la cara se va a Inactivar
                    if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                    {
                        PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;

                        EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                        EstructuraRedSurtidor[CaraEncuestada].Activa = false;

                        if (AplicaServicioWindows)
                        {
                            if (IniciarCambioTarjeta != null)
                            {
                                IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                            }
                        }
                        else
                        {
                            //Eventos.SolicitarIniciarCambioTarjeta( CaraID,  PuertoAImprimir);
                        }
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Inactivada en Fallo de Comunicacion");
                        SWRegistro.Flush();
                    }

                    //EGV:Si la cara se va a activar
                    if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                    {
                        PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;

                        EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                        string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " con fallo de comunicacion";
                        bool Imprime = true;
                        bool Terminal = false;
                        if (AplicaServicioWindows)
                        {
                            if (ExcepcionOcurrida != null)
                            {
                                ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                            }
                        }
                        //else
                        //{
                        //    //Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        //}
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar activacion: Fallo de comunicacion");
                        SWRegistro.Flush();
                    }

                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                            bool EstadoTurno = false;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            if (AplicaServicioWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }
                            //else
                            //{

                            //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                            bool EstadoTurno = true;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                            if (AplicaServicioWindows)
                            {
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                            }
                            //else
                            //{

                            //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }

                    //Ingresa a este condicional si el surtidor NO responde y si no se ha logueado aún la falla
                    if (ErrorComunicacion && !EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Perdida de comunicacion. Estado: " + EstructuraRedSurtidor[CaraEncuestada].Estado +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = true;
                    }

                    //Ingresa a este condicional cuando el surtidor responde y ya se había registrado una falla de comunicación
                    if (!ErrorComunicacion && EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Reestalecimiento de comunicacion, pero con errores en trama. Estado: " + EstructuraRedSurtidor[CaraEncuestada].Estado +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Reestablecimiento de comunicacion. Estado: " + EstructuraRedSurtidor[CaraEncuestada].Estado +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                try
                {
                   // VerificarConexion(); // DCF verificar coneccion 19-05-2016
                    Reconexion();
                
                    SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   Sockets.SocketException ");
                    SWTramas.Flush();

                }
                catch (Exception)
                {
                    //VerificarConexion();
                    Reconexion();
                    SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                    SWTramas.Flush();

                }

                string MensajeExcepcion = "Excepcion en el Metodo ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        public void 
            ArmarTramaTx(ComandoSurtidor ComandoTx, bool PrecioNivel1)
        {
            try
            {

                  //   bool Tipo; //True Escitura - False lectura
                Numeros_Registro = 1; 

                switch (ComandoTx)
                    {
                        case (ComandoSurtidor.Heartbeat1):  // se envia la autorizacion  bit 2 y bit 1 en UNO es el HeartBeat Alto
                              Register = 3155;   //Direcion del registro para los pulso de vida
                              Value_Registo =3; // 3 = 00000011// 1 envia el bit 1 en 00000001 y el bit 2 en 1 00000010 es el control de autorizacion 
                              Tipo = true; //Escritura
                             break;

                        case (ComandoSurtidor.Heartbeat0):  // se envia la autorizacion  bit 2 y bit 1  en CERO es el HeartBeat Bajo
                             Register = 3155;   //Direcion del registro para los pulso de vida
                             Value_Registo = 2; // 2 = 00000010// 0 envia el bit 1 en 00000010. se mantiene la autorizacion para no detener el despacho en el bit 2  00000010 
                             Tipo = true; //Escritura
                             break;






                        case (ComandoSurtidor.Volumen_Fill):
                             Register = 4719;   //
                             Value_Registo = 0; //
                             Numeros_Registro = 2; // Para tipo rela flot32
                             Tipo = false; //Lectura
                             break;






                        case (ComandoSurtidor.Autorizar):           //Autoriza Despacho
                              Register = 3155;   //
                              Value_Registo = 2 ; // 2 envia el bit 1 en 00000010
                              Tipo = true; //Escritura
                            break;

                        case (ComandoSurtidor.System_Alarma):       
                            Register = 3149;   //
                            Value_Registo = 1; // 
                            Tipo = false;//Lectura
                            break;

                        case (ComandoSurtidor.Power_Up):
                            Register = 3149;   //
                            Value_Registo = 2; // 
                            Tipo = false;//Lectura
                            break;

                        //Status:                    
                        case (ComandoSurtidor.Status_):
                            Register = 3149;   //
                            Value_Registo = 5; // 
                            Tipo = false;//Lectura
                            break;

                        case (ComandoSurtidor.Status_ESD):
                            Register = 3149;   //
                            Value_Registo = 3; // 
                            Tipo = false;//Lectura
                            break;

                        case (ComandoSurtidor.Status_Alarma):
                            Register = 3149;   //
                            Value_Registo = 4; // 
                            Tipo = false;//Lectura
                            break;
                        case (ComandoSurtidor.Status_Fulling):
                            Register = 3149;   //
                            Value_Registo = 5; // 
                            Tipo = false;//Lectura
                            break;

                        case (ComandoSurtidor.Status_Ready):
                            Register = 3149;   //
                            Value_Registo = 6; // 
                            Tipo = false;//Lectura
                            break;

                        case (ComandoSurtidor.Out_Pos_HB):
                            Register = 3149;   //
                            Value_Registo = 7; // 
                            Tipo = false;//Lectura
                            break;

                        //Tipos Real Flot32
                        case (ComandoSurtidor.Post_Volumen_TGT):
                            Register = 3255;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Mass_Fill):
                            Register = 4710; //4711;   //SERESTA 1 para que la dll pueda funcionar 
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Mass_Daily):
                            Register = 4713;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Mass_Last_Daily):
                            Register = 4715;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Mass_Total):
                            Register = 4717;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                       

                        case (ComandoSurtidor.Volumen_Daily):
                            Register = 4721;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Volumen_Last_Daily):
                            Register = 4723;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Volumen_Total):
                            Register = 4725;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Post_Pressure):
                            Register = 4727;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Gas_Density):
                            Register = 4729;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;

                        case (ComandoSurtidor.Post_Pressure_TGT):
                            Register = 4731;   //
                            Value_Registo = 0; //
                            Numeros_Registro = 2; // Para tipo rela flot32
                            Tipo = false; //Lectura
                            break;




                        case (ComandoSurtidor.TotalDespacho):       //Pide datos de Final de Despacho
                            TimeOut = 600;
                            BytesEsperados = 33;
                            BytesEsperados_Extended = 39;
                            break;

                        case (ComandoSurtidor.Totales):             //Pide Totalizadores
                            TimeOut = 800;
                            BytesEsperados = 94;
                            BytesEsperados_Extended = 130;   //Pump Sending 3 Grade 
                            break;

                        case (ComandoSurtidor.ParcialDespacho):     //Pide Parical de Venta = 0x60,
                            TimeOut = 350;
                            BytesEsperados = 6;
                            BytesEsperados_Extended = 8;
                            break;

                        case (ComandoSurtidor.DetenerTodos):        //Detiene todos los despachos
                            TimeOut = 80;//Antes 50
                            TramaTx[0] = Convert.ToByte(ComandoTx);
                            BytesEsperados = 0; 
                            BytesEsperados_Extended =0;
                            break;

                        case (ComandoSurtidor.CambiarPrecio):       //Cambio de Precio
                            
                            break;

                        case (ComandoSurtidor.PredeterminarVentaDinero): //Predetermina una venta con un valor especifico de Dinero 

                        

                            break;

                        case (ComandoSurtidor.PredeterminarVentaVolumen): //Predetermina una venta con un valor especifico de Metros cubicos o Galones


                            break;


                        case (ComandoSurtidor.EstadoExtendido):

                            break;
                    
                }
                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
               // eco = Convert.ToByte(TramaTx.Length);


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Comando " + ComandoTx + ":" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA EL COMANDO AL SURTIDOR
        public void EnviarComando()
        {
            try
            {
                if (PuertoCom.IsOpen)
                {

                    //Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                    PuertoCom.DiscardOutBuffer();
                    PuertoCom.DiscardInBuffer();

                    //Escribe en el puerto el comando a Enviar.  
                    PuertoCom.Write(TramaTx, 0, TramaTx.Length);

                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA TRANSMITIDA
                    string strTrama = "";
                    for (int i = 0; i <= TramaTx.Length - 1; i++)
                        strTrama += TramaTx[i].ToString("X2") + "|";

                    if (AplicaServicioTramas)
                    {

                        SWTramas.WriteLine(
                            DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                            DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                            DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                            DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                            "|" + CaraID + "|Tx|" + strTrama);

                        SWTramas.Flush();

                    }


                    //Thread.Sleep(TimeOut);//Tiempo 
                    Thread.Sleep(TimeOut);//para efecto de pruebas con surtidor virtual
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo EnviarComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //Envia comandos por TCPIP
        public void EnviarComando_TCPIP()
        {
            try
            {
                //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 6 " + CaraID);
                //SWRegistro.Flush();

               // using (ClienteFillPos_IMW = new TcpClient(DireccionIP, Puerto))

                VerificarConexion(); //DCF 19_05_2016 1612
               // Reconexion();
                
                 {
                    ModbusIpMaster  master = ModbusIpMaster.CreateIp(ClienteFillPos_IMW);

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 7 " + CaraID);
                    //SWRegistro.Flush();


                    Register = Convert.ToUInt16(Register - 1);//se resta un registro 

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 8 " + CaraID);
                    //SWRegistro.Flush();
                    //   bool Tipo; //True Escitura - False lectura
                    if (Tipo)
                    {
                      //escribir valor de registro
                      //Direcion PLC, Registro, Valor a escribir
                        //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 9 " + CaraID);
                        //SWRegistro.Flush(); 
                       
                      //master.WriteSingleRegister(CaraID, Register, Value_Registo);  //envia el comando por moodbus TCP IP   

                      master.WriteSingleRegister(CaraEncuestada, Register, Value_Registo);  //envia el comando por moodbus TCP IP   

                      //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 9-1 " + CaraID);
                      //SWRegistro.Flush(); 
                      SWTramas.WriteLine(

                          DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                          DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                          DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                          DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                          "|" + CaraID + "|Tx|" + "Register = " + (Register) + " -Value_Registo = " + Value_Registo + " -Numeros_Registro = " + Numeros_Registro);

                      SWTramas.Flush();
                    }
                    else
                    {

                        //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 10 " + CaraEncuestada);
                        //SWRegistro.Flush();
                    
                            //lee los valores de registro de entrada 
                        values = master.ReadHoldingRegisters(CaraEncuestada, Register, Numeros_Registro);

                       //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 11 " + CaraEncuestada);
                       //SWRegistro.Flush();

                       if (values.Length == 1)
                       {


                           //data1 = values[1];
                           //data2 = values[0];
                           //// Convert integer Values[] as a hex in a string variable
                           //hexValue1 = data1.ToString("X").PadRight(4, '0');
                           //hexValue2 = data2.ToString("X").PadRight(4, '0');


                           SWTramas.WriteLine(
                               DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                               DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                               DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                               DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                               "|" + CaraID + "|Tx|" + "Register = " + (Register) + "; Value_Registo[0] = " + values[0] );

                           SWTramas.Flush();
                       }

                       if (values.Length == 2)
                       {


                           data1 = values[1];
                           data2 = values[0];
                           // Convert integer Values[] as a hex in a string variable
                           hexValue1 = data1.ToString("X").PadRight(4, '0');
                           hexValue2 = data2.ToString("X").PadRight(4, '0');


                           SWTramas.WriteLine(
                               DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                               DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                               DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                               DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                               "|" + CaraID + "|Tx|" + "Register = " + (Register) + "; Value_Registo[0] = " + values[0] + "; Value_Registo[1] = " + values[1]);

                           SWTramas.Flush();
                       }
                    }

                }
                
                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA            

                 //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 111 " + CaraID);
                 //SWRegistro.Flush();
             
                Thread.Sleep(300);//Tiempo de espera

            }
            catch (System.IO.IOException ex)
            {
                Reconexion(); 

                SWRegistro.WriteLine(DateTime.Now + "|Error |" + " Exception en EnviarComando_TCPIP() " + ex.Message);
                SWRegistro.Flush();
                Thread.Sleep(200);
            }
        }

        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }
        
        public void Reconexion() // DCF 25-05-2016
        {
              try
                    {
                        bool Conectado = false;
                        int conte = 10;
                        int reint = 0;


                        EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstadoCara.Indeterminado;


                        while (!Conectado)
                        {
                            try
                            {

                                if (ClienteFillPos_IMW != null)
                                {  //********************************************
                                    //DESCONEXION 

                                    Stream.Close();
                                    Stream.Dispose();
                                    Stream = null;
                                    ClienteFillPos_IMW.Close();
                                    ClienteFillPos_IMW = null;



                                    string MensajeExcepcion = "|0|Conexión Close : " + DireccionIP + ": " + Puerto;
                                    SWRegistro.WriteLine(DateTime.Now + "" + MensajeExcepcion);
                                    SWRegistro.Flush();
                                    //********************************************
                                }
                                else
                                {

                                    ////Crea y abre la conexión con el Servidor
                                    ClienteFillPos_IMW = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                                    Stream = ClienteFillPos_IMW.GetStream();
                                    Conectado = true;

                                    if (ClienteFillPos_IMW != null)
                                    {
                                        string MensajeExcepcion = "|0|Conexión con el Server: " + DireccionIP + ": " + Puerto;
                                        SWRegistro.WriteLine(DateTime.Now + "" + MensajeExcepcion);
                                        SWRegistro.Flush();
                                    }
                                    else
                                    {
                                        string MensajeExcepcion = "|0|No conexión con el ServerXX: " + DireccionIP + ": " + Puerto;
                                        SWRegistro.WriteLine(DateTime.Now + "" + MensajeExcepcion);
                                        SWRegistro.Flush();

                                    }
                                }

                            }
                            catch 
                            {
                                Conectado = false;

                                Thread.Sleep(500);//Tiempo de espera

                                if (conte > 10 )
                                {
                                    string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + "Reintento2: " + reint;
                                    SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                                    SWRegistro.Flush();
                                    reint++;

                                    conte = 0; 
                                }
                                conte++;                             
                            }
                        }

                      //  ModbusIpMaster master = ModbusIpMaster.CreateIp(ClienteFillPos_IMW);

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }

          }

        
        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteFillPos_IMW == null)
                {
                    Boolean EsInicializado = false;
                    SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*7|Verificando conexion 1 " + EsInicializado);

                    SWTramas.Flush();
                    while (!EsInicializado)
                    {
                        try
                        {
                            SWTramas.WriteLine(
DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
"|" + CaraID + "|*8|Verificando conexion 2 " + EsInicializado);

                            SWTramas.Flush();
                            ClienteFillPos_IMW = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteFillPos_IMW == null)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|No inicializo - Ip: " + DireccionIP + " Puerto: " + Puerto);
                                SWRegistro.Flush();
                                Thread.Sleep(200);
                            }
                        }
                        catch (Exception e)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla de inicializacion - Ip: " + DireccionIP + " Puerto: " + Puerto + " Mensaje: " + e.Message);
                            SWRegistro.Flush();
                            Thread.Sleep(200);
                        }

                        if (ClienteFillPos_IMW != null)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            //SWRegistro.Flush();
                            EsInicializado = true;
                        }
                    }
                    SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexio 4");

                    SWTramas.Flush();
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteFillPos_IMW.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteFillPos_IMW.Client.BeginDisconnect(true, callBack, ClienteFillPos_IMW);

                    }

                    catch (Exception e)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginDisconnect: " + e.Message);
                        SWRegistro.Flush();
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");
                    //SWRegistro.Flush();
                    estadoAnterior = true;
                }



                while (!this.ClienteFillPos_IMW.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteFillPos_IMW.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteFillPos_IMW);
                        //ClienteFillPos_IMW.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteFillPos_IMW.Client.Connected)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {//Reintento de conexcion para el caso de Cruz roja

                        //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect-Creando Socket: " + ex.Message);
                        //SWRegistro.Flush();
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect-Creando Socket: Abriendo nuevamente la conexcion");
                        SWRegistro.Flush();

                        AbrirSocketReintento();

                    }
                    catch (Exception)
                    {
                        //SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect: " + e.Message);
                        //SWRegistro.Flush();

                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect: Abriendo nuevamente la conexcion");
                        SWRegistro.Flush();

                        AbrirSocketReintento();
                    }
                }
                this.Stream = ClienteFillPos_IMW.GetStream();
                if (!estadoAnterior)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");
                    SWRegistro.Flush();
                }
            }
            catch (Exception exec)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);
                SWRegistro.Flush();
            }
        }


        void LimpiarVariableSocket()
        {
            try
            {
                ClienteFillPos_IMW.Close();
                Stream.Close();
                Stream.Dispose();
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Metodo|" + "LimpiarVariableSocket: " + "Mensaje: " + ex.Message);
                SWRegistro.Flush();
            }

        }

        void AbrirSocketReintento()
        {
            try
            {
                Thread.Sleep(20);
                LimpiarVariableSocket();//Libero los recursos antes de iniciar una nueva conexcion con la veeder
                ClienteFillPos_IMW = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteFillPos_IMW.GetStream();
                if (this.ClienteFillPos_IMW.Client.Connected == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Conexion Abierta");
                    SWRegistro.Flush();
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Conexion Cerrada");
                    SWRegistro.Flush();
                }


            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Falla AbrirSocketReintento  Creando Socket : " + ex.Message);
                SWRegistro.Flush();

            }

        }

        public void RecibirInformacion_TCPIP()
        {
            try
            {

                ModbusIpMaster master = ModbusIpMaster.CreateIp(ClienteFillPos_IMW);



           switch (ComandoCaras)
           {

             case (ComandoSurtidor.Heartbeat1):
             case (ComandoSurtidor.Status_):

                 #region comando de pregunta 1 byte de respuesta

                 Register = Convert.ToUInt16(3149 - 1);//se resta u Register = 3149; en el registro se dan las respuesta a las consultas                
                    

                            //lee los valores de registro de entrada 
                         values = master.ReadHoldingRegisters(CaraID, Register, Numeros_Registro);


                         ////Se separan el Codigo del estado y la cara en variables diferentes.  La "e" es el parametro aditivo del ECO recibido
                         //byte CodigoTramaRX = Convert.ToByte(values[0]);
                         //   string RX  ="";

                         //if ((CodigoTramaRX & 1) == 1) //registro 43149.1
                         //{
                         //   RX = " System Alarma";

                         //}


                         //if ((CodigoTramaRX & 2) == 2) //registro 43149.2
                         //{
                         //     RX = " PLC Power_UP Registro 43149.2"; 
                         //}


                         //if ((CodigoTramaRX & 4) == 4) //registro 43149.3
                         //{
                         //   RX =" STATUS_ESD Registro 43149.3";
                         //}


                         //if ((CodigoTramaRX & 8) == 8) //registro 43149.4
                         //{
                         //     RX =" STATUS_ALARMA Registro 43149.4";                            
                         //}



                         //if ((CodigoTramaRX & 32) == 32) //registro 43149.6
                         //{
                         //    RX = "Status_Ready Registro 43149.6 ";
                         //}


                         //if ((CodigoTramaRX & 16) == 16) //registro 43149.5
                         //{
                         //    RX = "Status_Fueling Registro 43149.5";
                         //}



                // escribir en el log de tramas 
                            SWTramas.WriteLine(
                                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                "|" + CaraID + "|Rx|" + values[0]);

                            SWTramas.Flush();
                        break;

                 #endregion;

          }

                        /////////////////////////////////////////////////////////////////////////////////

            AnalizarTrama();

                    
                  if (values == null)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        SWRegistro.Flush();

                        ErrorComunicacion = true;
                    } 
            }
            catch (Exception Excepcion)
            {
                LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();

                Reconexion();


            }
        }



        static string Obtener_Valor_Float(string Hex32Input)
        {
            try
            {
                string doublestr = "";
                UInt64 bigendian;
                bool success = UInt64.TryParse(Hex32Input,
                    System.Globalization.NumberStyles.HexNumber, null, out bigendian);
                if (success)
                {
                    double fractionDivide = Math.Pow(2, 23);
                    double doubleout;

                    int sign = (bigendian & 0x80000000) == 0 ? 1 : -1;
                    Int64 exponent = ((Int64)(bigendian & 0x7F800000) >> 23) - (Int64)127;
                    UInt64 fraction = (bigendian & 0x007FFFFF);
                    if (fraction == 0)
                        doubleout = sign * Math.Pow(2, exponent);
                    else
                        doubleout = sign * (1 + (fraction / fractionDivide)) * Math.Pow(2, exponent);
                    doublestr = doubleout.ToString("N6");
                }
                return doublestr;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el CalcularCRC: " + Excepcion;
                MessageBox.Show(MensajeExcepcion);

                return "Excepción";
            }

        }


        public void LimpiarSockets()
        {
            try
            {
                //ClienteFillPos_IMW.Client.Disconnect(false);  
                ClienteFillPos_IMW.Client.Close();
                ClienteFillPos_IMW.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteFillPos_IMW = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }




        //LEE Y ALMACENA LA TRAMA RECIBIDA
        public void RecibirInformacion()
        {
            try
            {

                if (PuertoCom.IsOpen)
                {
                    Bytes_leidos = PuertoCom.BytesToRead;

                    if (!TramaEco)
                        eco = 0;

                    //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                    BytesEsperados = BytesEsperados + eco;
                    BytesEsperados_Extended = BytesEsperados_Extended + eco;
                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes_leidos == BytesEsperados || Bytes_leidos == BytesEsperados_Extended)
                    {
                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                        PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length - eco];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                            TramaRx[i] = TramaTemporal[i + eco];


                        ///////////////////////////////////////////////////////////////////////////////
                        //LOGUEO DE TRAMA RECIBIDA
                        string strTrama = "";
                        for (int i = 0; i <= TramaRx.Length - 1; i++)
                            strTrama += TramaRx[i].ToString("X2") + "|";


                        if (this.AplicaServicioTramas)
                        {
                            SWTramas.WriteLine(
                                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                "|" + CaraID + "|Rx|" + strTrama);

                            SWTramas.Flush();

                        }

                        /////////////////////////////////////////////////////////////////////////////////

                        AnalizarTrama();

                    }
                    else if (ErrorComunicacion == false)
                        ErrorComunicacion = true;

                    //SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        public void AnalizarTrama()
        {
            try
            {
                switch (ComandoCaras)
                {

                    case (ComandoSurtidor.Heartbeat1):
                      
                         out_pos_hb = Convert.ToInt32(values[0]) & 64;                  

                         if (out_pos_hb == 64)//64 = 0100 000
                         {
                             ErrorComunicacion = false;
                            // EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Heartbeat_Horner;
                             //HB OK se actia en 1 registro 3149.7  = 0100 0000 = 0x60= d96
                         }
                         else
                             ErrorComunicacion = true;
                        break;


                    case (ComandoSurtidor.Heartbeat0):
                       

                        out_pos_hb = Convert.ToInt32(values[0]) & 64;

                        if (out_pos_hb != 64)//64 = 0100 000
                        {
                            ErrorComunicacion = false;
                           // EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Heartbeat_Horner;
                            //HB OK se actia en 1 registro 3149.7  = 0100 0000 = 0x60= d96
                        }

                        break;

                    case (ComandoSurtidor.Status_):
                        AsignarEstado();
                        break;
                        
                    case (ComandoSurtidor.Volumen_Total): //5
                        RecuperarTotalizadores();
                        break;


                    case (ComandoSurtidor.Volumen_Fill):
                        RecuperarParcialesdeVenta();
                        break;


                    case(ComandoSurtidor.Post_Pressure_TGT):
                        RecuperarPresion_TGT();
                        break;



                    case (ComandoSurtidor.TotalDespacho): // 4
                        RecuperarDatosFindeVenta();
                        break;
                  
                    case (ComandoSurtidor.ParcialDespacho):// 6
                        RecuperarParcialesdeVenta();
                        break;
                    case (ComandoSurtidor.EnviarDatos):
                        ConfirmacionEnvioDatos();
                        break;
                    case (ComandoSurtidor.EstadoExtendido):
                        EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = Convert.ToByte(0x0F & (TramaRx[14] - 1)); //OJO asignacion de grado ******************
                        break;
                }



            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AnalizarTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        public void AsignarEstado()
        {
            try
            {
                //Se separan el Codigo del estado y la cara en variables diferentes.  La "e" es el parametro aditivo del ECO recibido
                byte CodigoEstado = Convert.ToByte( values[0] );


                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;



                if ((CodigoEstado & 1) == 1 ) //registro 43149.1
                {

                    if (Contador_Visula1 <= 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Alarma de Sistema PLC Registro 43149.1");
                        SWRegistro.Flush();
                    }
                    Contador_Visula0++;

                    if (Contador_Visula0 > 2000)
                        Contador_Visula0 = 0; 

                }


                if ((CodigoEstado & 2) == 2) //registro 43149.2
                {
                    if(Contador_Visula1 <= 0)
                   {

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|PLC Power_UP Registro 43149.2");
                    SWRegistro.Flush();
                   }

                    Contador_Visula1++;
                    if (Contador_Visula1 > 20000)
                        Contador_Visula1 = 0; 

                }


                if ((CodigoEstado & 4) == 4) //registro 43149.3
                {

                    if (Contador_Visula2 <= 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|STATUS_ESD Registro 43149.3");
                        SWRegistro.Flush();
                    }
                       Contador_Visula2++;
                       if (Contador_Visula2 > 2000)
                           Contador_Visula2 = 0;

                }


                if ((CodigoEstado & 8) == 8) //registro 43149.4
                {
                   

                    if (Contador_Visula3 <= 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|STATUS_ALARMA Registro 43149.4");
                        SWRegistro.Flush();
                    }
                    Contador_Visula3++;
                    if (Contador_Visula3 > 2000)
                        Contador_Visula3 = 0;

                }

                if((CodigoEstado & 64) == 64)
                {

                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Horner_Out_Pos_HB;
                }

                if ((CodigoEstado & 32) == 32) //registro 43149.6
                {
                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Horner_Status_Ready;

                    if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                    {
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Finaliza venta en Estado Espera");
                        SWRegistro.Flush();
                        //EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                    }
                    else
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;
                  
                }
                else
                {

                    //Almacena en archivo el estado actual del surtidor
                    //if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    //{
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| el Fill Pos no esta Listo" );
                    //    SWRegistro.Flush();
                    //}

                }


                if ((CodigoEstado & 16) == 16) //registro 43149.5
                {
                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despachando;
                }


                InconsistenciaDatosRx = false; //No hubo error por fallas en datos


                    //Almacena en archivo el estado actual del surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado +
                            " - " + CodigoEstado.ToString("X2").PadLeft(2, '0'));
                        SWRegistro.Flush();
                    }
                }
              
            
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AsignarEstado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        public void TomarAccion()
        {
            try
            {
                int Reintentos = 0;
                //Puerto utilizado por el autorizador para imprimir mensajes de error
                String PuertoAImprimir;


                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado );
                //SWRegistro.Flush();

                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {

                    /***************************ESTADO EN ESPERA***************************/
                    #region Estado en Espera
                    case (EstadoCara.Espera):


                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            if (AplicaServicioWindows)
                            {
                                if (IniciarCambioTarjeta != null)
                                {
                                    IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.SolicitarIniciarCambioTarjeta( CaraID,  PuertoAImprimir);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Inactivacion en Estado Espera");
                            SWRegistro.Flush();

                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante inactivación
                            if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara Inactivada";
                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = false;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                    if (AplicaServicioWindows)
                                    {
                                        if (CancelarProcesarTurno != null)
                                        {
                                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                        }
                                    }
                                    //else
                                    //{

                                    //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                    //}
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                                {
                                    //Se establece valor de la variable para que indique que ya fue reportado el error
                                    EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                    bool EstadoTurno = true;
                                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    if (AplicaServicioWindows)
                                    {
                                        if (CancelarProcesarTurno != null)
                                        {
                                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                        }
                                    }
                                    //else
                                    //{

                                    //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                    //}
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                            }

                            //Sale del Caso si se inactiva
                            break;
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            ArrayLecturas = TomarLecturaActivacionCara();
                            if (ArrayLecturas.Count > 0)
                            {
                                //Instancia Array para reportar las lecturas
                                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                                ArrayLecturas.CopyTo(LecturasEnvio);


                                if (AplicaServicioWindows)
                                {
                                    if (LecturasCambioTarjeta != null)
                                    {
                                        LecturasCambioTarjeta(LecturasEnvio);
                                    }
                                }
                                else
                                    //{
                                    //    //Lanza Evento para reportar las lecturas después de un cambio de tarjeta
                                    //    Eventos.SolicitarLecturasCambioTarjeta( LecturasEnvio);
                                    //}
                                    //Inicializa bandera que indica la activación de una cara
                                    EstructuraRedSurtidor[CaraEncuestada].ActivarCara = false;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Activacion en Estado Espera. Lectura: " + LecturasEnvio);
                                SWRegistro.Flush();

                                //EGV: Mando a cambiar los precios de la cara
                                CambiarPrecios(EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count * 2);
                            }
                        }

                        //Informa cambio de estado
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                            if (AplicaServicioWindows)
                            {
                                if (CaraEnReposo != null)
                                {
                                    CaraEnReposo(CaraID, mangueraColgada);
                                }
                            }
                            //else
                            //{
                            //    Eventos.InformarCaraEnReposo( CaraID,  mangueraColgada);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cara en Espera. Manguera: " + mangueraColgada);
                            SWRegistro.Flush();

                            //Si había venta por predeterminar, al colgar la manguera el sistema cancela el proceso de predeterminado
                            if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                                EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen = false;
                            if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                                EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia toma de lecturas para cierre o apertura");
                            SWRegistro.Flush();
                            LecturaAperturaCierre();
                        }

                        //Revisa si se tiene que hacer cambio de producto en alguna manguera de la cara
                        if (EstructuraRedSurtidor[CaraEncuestada].CambiarProductoAMangueras)
                        {
                            //Revisando en que grados hay que cambiar el producto
                            foreach (Grados OGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                            {
                                //Si hay cambio de producto se realiza el cambio de precio
                                if (OGrado.CambiarProducto)
                                {
                                    //Si se aplica satisfactoriamente el cambio de precio se hace efectivo el cambio de producto
                                    if (CambiarPreciosEnGrado(OGrado))
                                    {
                                        int MangueraANotificar = OGrado.MangueraBD;
                                        OGrado.Autorizar = true;
                                        OGrado.IdProducto = OGrado.IdProductoACambiar;
                                        if (AplicaServicioWindows)
                                        {
                                            if (NotificarCambioPrecioManguera != null)
                                            {
                                                NotificarCambioPrecioManguera(MangueraANotificar);
                                            }
                                        }
                                        //else
                                        //{

                                        //    Eventos.SolicitarNotificarCambioPrecioManguera( MangueraANotificar);
                                        //}
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cambio de precio Manguera: " +
                                            OGrado.MangueraBD + " - Grado: " + OGrado.IdProducto);
                                        SWRegistro.Flush();
                                    }
                                    else
                                    {
                                        //Se informa el estado de no autorizacion para la manguera a la cual no se le pudo
                                        //hacer el cambio de precio
                                        //OGrado.Autorizar = false;
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Informa cambio de precio Manguera: " +
                                           OGrado.MangueraBD + ", Desactivada por problema al cambiar producto");
                                        SWRegistro.Flush();
                                    }
                                    OGrado.CambiarProducto = false;
                                }
                            }

                            EstructuraRedSurtidor[CaraEncuestada].CambiarProductoAMangueras = false;
                        }
                        break;
                    #endregion

                    /***************************ESTADO EN DESPACHO***************************/
                    #region Estado Despacho
                    case (EstadoCara.Despachando):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraID + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;

                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Inactivacion: Cara en despacho");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Activacion: Cara en despacho");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa fallo en toma de Lecturas Inciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                        //Pedir Parciales de Venta
                        ComandoCaras = ComandoSurtidor.Volumen_Fill;
                        ArmarTramaTx(ComandoSurtidor.Volumen_Fill, false);

                        if (EsTCPIP)
                        {
                            EnviarComando_TCPIP();

                            RecibirInformacion_TCPIP();
                        }

                       
                        //Dispara evento al programa principal si no hubo fallo en la recepcion de los datos
                        if (ErrorComunicacion == false)
                        {
                            string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");

                            //Eventos.InformarVentaParcial( CaraID,  strTotalVenta,  strVolumen);

                            string[] DatosVentaParcial = { CaraID.ToString(), strTotalVenta.ToString(), strVolumen.ToString() };

                            ThreadPool.QueueUserWorkItem(new WaitCallback(InfoVentaParcial), DatosVentaParcial);

                            //Thread HiloVentaParcial = new Thread(InfoVentaParcial);
                            //HiloVentaParcial.Start(DatosVentaParcial); //21/04/2012 DCF hilo para el envio de datos en Venta parciales 

                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara = false;
                            ProcesoEnvioComando(ComandoSurtidor.Detener, false);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Enviado Comando de Detener por monitoreo");
                            SWRegistro.Flush();
                        }
                        break;
                    #endregion

                    /***************************ESTADO DETENIDO***************************/
                    #region Estado Detenido
                    case (EstadoCara.Detenido):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            if (AplicaServicioWindows)
                            {
                                if (IniciarCambioTarjeta != null)
                                {
                                    IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.SolicitarIniciarCambioTarjeta( CaraID,  PuertoAImprimir);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Inactivacion en Estado Detenido");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " en estado Detenido";
                            bool Imprime = true;
                            bool Terminal = false;

                            //JUAN DAVID
                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar activacion: Cara en estado Detenido");
                            SWRegistro.Flush();
                            break;
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Detenida");
                            SWRegistro.Flush();
                        }
                        break;
                    #endregion

                    /***************************ESTADO EN FIN DE DESPACHO A***************************/
                    /***************************ESTADO EN FIN DE DESPACHO B***************************/
                    /***************************ESTADO EN FIN DE DESPACHO FORZADO***************************/
                    #region Estados Fin de Despacho
                    case (EstadoCara.FinDespachoA):
                    case (EstadoCara.FinDespachoB):
                    case (EstadoCara.FinDespachoForzado):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraID + " en Fin de Venta";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;

                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Inactivacion: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " en Fin de Despacho";
                            bool Imprime = true;
                            bool Terminal = false;

                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Activacion: Cara en estado Fin de Venta");
                            SWRegistro.Flush();
                            break;
                        }

                        //EGV:Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                            //SW.WriteLine(DateTime.Now + "  Cara " + CaraEncuestada + ": proceso de fin de venta lanzado ESVENTAPARCIAL: " + Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial));
                            //{
                            //Thread HiloVenta = new Thread(ProcesoFindeVenta);
                            //HiloVenta.Start();
                            ProcesoFindeVenta();
                        //}
                        break;
                    #endregion

                    /***************************ESTADO ERROR***************************/
                    #region Estado de Error
                    case (EstadoCara.Error):
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Error");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            if (AplicaServicioWindows)
                            {
                                if (IniciarCambioTarjeta != null)
                                {
                                    IniciarCambioTarjeta(CaraID, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.SolicitarIniciarCambioTarjeta( CaraID,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Inactivacion en Estado de Error");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " en estado de Error";
                            bool Imprime = true;
                            bool Terminal = false;
                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar activacion: Cara en estado de Error");
                            SWRegistro.Flush();
                            break;
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en estado de ERROR";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Envia comando Totales para desbloquear");
                        SWRegistro.Flush();
                        ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                        break;
                    #endregion

                    /***************************ESTADO POR AUTORIZAR***************************/
                    #region Estado Por Autorizar
                    case (EstadoCara.PorAutorizar):
                        //EGV:Si la cara se va a Inactivar
                        if (EstructuraRedSurtidor[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            string Mensaje = "No se puede ejecutar inactivacion: Cara " + CaraID + " en intento de autorizacion";
                            bool Imprime = true;
                            bool Terminal = false;
                            EstructuraRedSurtidor[CaraEncuestada].InactivarCara = false;
                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Inactivacion: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                        }

                        //EGV:Si la cara se va a activar
                        if (EstructuraRedSurtidor[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            EstructuraRedSurtidor[CaraEncuestada].Activa = false;
                            string Mensaje = "No se puede ejecutar activacion: Cara " + CaraID + " en estado Por Autorizar";
                            bool Imprime = true;
                            bool Terminal = false;
                            if (AplicaServicioWindows)
                            {
                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                }
                            }
                            //else
                            //{
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se puede ejecutar Activacion: Cara en estado Por Autorizar");
                            SWRegistro.Flush();
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está descolgada
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Manguera descolgada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }

                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado &&
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                        {

                            //DCF_Extended   7 --- CALL
                            // Recuperar Grado por autorizar
                            #region _Extended -- estado = 7 CALL
                            //if (CaraEncuestada == 0x03 || CaraEncuestada == 0x04 ||
                            //    CaraEncuestada == 0x05 || CaraEncuestada == 0x06)//SOlo el surtidor 2 cara 3 -4
                            if (EstructuraRedSurtidor[CaraEncuestada].Gilbarco_Extended)

                            //if (CaraEncuestada == 0x01 || CaraEncuestada == 0x02 ||
                            //   CaraEncuestada == 0x03 || CaraEncuestada == 0x04 ||
                            //   CaraEncuestada == 0x05 || CaraEncuestada == 0x06 ||
                            //   CaraEncuestada == 0x07 || CaraEncuestada == 0x08 ||
                            //   CaraEncuestada == 0x09 || CaraEncuestada == 0x10 ||
                            //   CaraEncuestada == 0x11 || CaraEncuestada == 0x12)// para los 6 surtidores 
                            {
                                //por efecto de pruebas comprobar como regresa el grado en el byte 1 
                                // EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = TramaRx[1];//DCF_Extended

                                Reintentos = 0;
                                do
                                {
                                    if (RecuperarEstadoExtendido()) // recupera el Grado que se levanta o el que se va Autorizar **** faltaba 05/06/2012 2838
                                    {
                                        //Revisa si el grado se encuentra configurado
                                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >=
                                            EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma lecturas iniciales para Validacion de Ventas Fuera de Sistema con Gilbarco_Extended ");
                                            SWRegistro.Flush();

                                            //Obtiene la Lectura Inicial de la Venta del Grado que ha sido AUTORIZADO
                                            TomarLecturas();

                                            EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado;

                                            //Almacena las lecturas actuales de cada grado como Lecturas Iniciales
                                            foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].LecturaInicialVenta =
                                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].Lectura;

                                            int IdProducto =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;
                                            int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                                            string Lectura = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura.ToString("N3");


                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                                + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                                IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                            SWRegistro.Flush();


                                            // Eventos.RequerirAutorizacion( CaraID,  IdProducto,  IdManguera,  Lectura);
                                            string[] DatosAutorizacion = { CaraID.ToString(), IdProducto.ToString(), IdManguera.ToString(), Lectura };

                                            //Thread HiloAutorizacion = new Thread(PeticionAutorizacion);
                                            //HiloAutorizacion.Start(DatosAutorizacion);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(PeticionAutorizacion), DatosAutorizacion);
                                            break;
                                        }
                                        else
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Intento de autorizacion en grado Inexistente (Grado " +
                                                EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + ")");
                                            SWRegistro.Flush();
                                        }
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Surtidor no responde Estado Extendido 1. Reintentos " +
                                            Reintentos);
                                        SWRegistro.Flush();
                                    }


                                    Reintentos++;
                                } while (Reintentos <= 3);

                            }
                            #endregion;

                            else
                            {
                                Reintentos = 0;
                                do
                                {
                          
                                    if (RecuperarEstadoExtendido()) // recupera el Grado que se levanta o el que se va Autorizar 
                                    {
                                        //Revisa si el grado se encuentra configurado
                                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >=
                                            EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Toma lecturas iniciales para Validacion de Ventas Fuera de Sistema");
                                            SWRegistro.Flush();

                                            //Obtiene la Lectura Inicial de la Venta del Grado que ha sido AUTORIZADO
                                            TomarLecturas();
                                            EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado;

                                            //Almacena las lecturas actuales de cada grado como Lecturas Iniciales
                                            foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].LecturaInicialVenta =
                                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].Lectura;

                                            int IdProducto =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;
                                            int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                                            string Lectura = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura.ToString("N3");


                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                                + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                                IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                            SWRegistro.Flush();


                                            // Eventos.RequerirAutorizacion( CaraID,  IdProducto,  IdManguera,  Lectura);
                                            string[] DatosAutorizacion = { CaraID.ToString(), IdProducto.ToString(), IdManguera.ToString(), Lectura };

                                            //Thread HiloAutorizacion = new Thread(PeticionAutorizacion);

                                            //HiloAutorizacion.Start(DatosAutorizacion);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(PeticionAutorizacion), DatosAutorizacion);

                                            break;
                                        }
                                        else
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Intento de autorizacion en grado Inexistente (Grado " +
                                                EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + ")");
                                            SWRegistro.Flush();
                                        }
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Surtidor no responde Estado Extendido 1. Reintentos " +
                                            Reintentos);
                                        SWRegistro.Flush();
                                    }
                                    Reintentos++;
                                } while (Reintentos <= 3);
                            }
                        }

                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        else if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                        {
                            //cambio de precio para un cliente X
                            if (EstructuraRedSurtidor[CaraEncuestada].AplicaCambioPrecioCliente)
                            {
                                 EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta /
                                        EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;

                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 0)
                                {
                                    if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta != 0 &&
                                        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta !=
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1)
                                    {
                                        if (CambiarPrecioVenta(EstructuraRedSurtidor[CaraEncuestada].PrecioVenta))
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Autorizacion cambio precio OK ");
                                            SWRegistro.Flush();
                                        }
                                        else
                                        {
                                            PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                                            string Mensaje = "Error en el cambio de Precio por cliente, Autorizacion fallida.";
                                            bool Imprime = true;
                                            bool Terminal = false;
                                            if (AplicaServicioWindows)
                                            {
                                                if (ExcepcionOcurrida != null)
                                                {
                                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                                }
                                            }

                                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Autorizacion cambio precio Fallido ");
                                            SWRegistro.Flush();

                                            break;
                                        }
                                    }
                                }
                            }

                            //SE VERIFICA QUE LA MANGUERA DESCOLGADA SEA LA MISMA QUE SE MANDÓ A CALIBRAR
                            if (EstructuraRedSurtidor[CaraEncuestada].MangueraProgramada != -1 &&
                                EstructuraRedSurtidor[CaraEncuestada].MangueraProgramada != EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD)
                            {
                                PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                                string Mensaje = "LA MANGUERA DESCOLGADA NO ES LA MISMA QUE SE MANDO A CALIBRAR.";
                                bool Imprime = true;
                                bool Terminal = false;
                                if (AplicaServicioWindows)
                                {
                                    if (ExcepcionOcurrida != null)
                                    {
                                        ExcepcionOcurrida(Mensaje, Imprime, Terminal, PuertoAImprimir);
                                    }
                                }
                                //else
                                //{
                                //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                                //}


                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Grado " + EstructuraRedSurtidor[CaraEncuestada].GradoCara +
                                    ". Se denego la autorizacion porque la manguera descolgada no era la predeterminada");
                                SWRegistro.Flush();
                                break;
                            }

                            //Revisando que se haya bloqueado la manguera por error en cambio de producto
                            //if (!EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Autorizar)
                            //{
                            //    PuertoAImprimir = EstructuraRedSurtidor[CaraEncuestada].PuertoParaImprimir;
                            //    string Mensaje = "LA MANGUERA HA SIDO BLOQUEADA POR EL SISTEMA. POR FAVOR COMUNIQUESE CON EL SOPORTE TECNICO.";
                            //    bool Imprime = true;
                            //    bool Terminal = false;
                            //    Eventos.ReportarExcepcion( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            //    EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Grado " + EstructuraRedSurtidor[CaraEncuestada].GradoCara +
                            //        ". Se denego la autorizacion porque no se pudo cambiar el producto");
                            //    SWRegistro.Flush();
                            //    break;
                            //}

                            //Confirma Grado que requiere autorizacion
                            bool ConfirmacionGradoAutorizado = ConfirmacionGrado(EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado);
                            if (!ConfirmacionGradoAutorizado && EstructuraRedSurtidor[CaraEncuestada].EsVentaGerenciada)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Manguera que intenta despachar NO fue la autorizada");
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CambioMangueraEnVentaGerenciada != null)
                                    {
                                        CambioMangueraEnVentaGerenciada(CaraID);
                                    }
                                }
                                //else
                                //{
                                //    Eventos.InformarCambioMangueraEnVentaGerenciada( CaraID);
                                //}
                            }
                            else
                            {
                                string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");

                                if (AplicaServicioWindows)
                                {
                                    if (LecturaInicialVenta != null)
                                    {

                                        LecturaInicialVenta(CaraID, strLecturasVolumen);
                                    }
                                }
                                //else
                                //{
                                //    Eventos.InformarLecturaInicialVenta( CaraID,  strLecturasVolumen);
                                //}

                                //Loguea Evento de envio de lectura
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Venta: " +
                                    strLecturasVolumen);
                                SWRegistro.Flush();

                                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1;


                                //Si la siguiente venta es predeterminada, realiza el proceso de programación
                                if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado > 0)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Programacion de Venta");
                                    SWRegistro.Flush();
                                    if (Predeterminar())
                                    {
                                        //Envía comando de Autorización
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Autorizacion de Venta Predeterminada");
                                        SWRegistro.Flush();
                                        Reintentos = 0;
                                        
                                        // ****************************************************************************************************************
                                        //pregunta si el estado de la manguera es Igula a Reposo si es true se sale del proceso de autorizacion. 
                                        ProcesoEnvioComando(ComandoSurtidor.Estado, false);

                                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                                        {
                                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Autorizacion Anulada en Venta Predeterminada, Cara: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                            SWRegistro.Flush();

                                            break;
                                        }
                                        // ****************************************************************************************************************
                      

                                        do
                                        {
                                            ProcesoEnvioComando(ComandoSurtidor.Autorizar, false);                                            
                                            Reintentos++;
                                            Thread.Sleep(30);

                                            if (EsTCPIP)
                                            {
                                                RecibirInformacion_TCPIP();
                                            }

                                            ProcesoEnvioComando(ComandoSurtidor.Estado, false);
                                        } while (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizado &&
                                            EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho && (Reintentos <= 3));

                                        //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                            EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                                        {
                                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                                        }
                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion rechazada");
                                        SWRegistro.Flush();
                                    }
                                }
                                else
                                {
                                    //Envía comando de Autorización
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Autorizacion de Venta");
                                    SWRegistro.Flush();
                                    Reintentos = 0;

                                    // ****************************************************************************************************************
                                    //pregunta si el estado de la manguera es Igula a Reposo si es true se sale del proceso de autorizacion. 
                                    ProcesoEnvioComando(ComandoSurtidor.Estado, false);

                                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Espera)
                                    {
                                        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Autorizacion Anulada Cara: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                        SWRegistro.Flush();

                                        break;
                                    }
                                    // ****************************************************************************************************************
                      

                                    do
                                    {
                                        ProcesoEnvioComando(ComandoSurtidor.Autorizar, false);
                                        Reintentos++;
                                        Thread.Sleep(30);

                                        if (EsTCPIP)
                                        {
                                            RecibirInformacion_TCPIP();
                                        }

                                        ProcesoEnvioComando(ComandoSurtidor.Estado, false);
                                    } while (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizado &&
                                        EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho && (Reintentos <= 3));

                                    //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                                    {
                                        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                                    }
                                }
                            }
                        }
                    //    SWTramas.WriteLine(
                    //DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    //DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    //DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    //DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    //"|" + CaraID + "|Tx|Fin Por Autorizar");

                SWTramas.Flush();
                        break;
                    #endregion

                    case (EstadoCara.Autorizado):

                        break;

                    case(EstadoCara.Horner_Status_Ready):

                        break;

                    case (EstadoCara.Horner_Out_Pos_HB):


                        break; 

                    

                        
                        #region Default
                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Default: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                        SWRegistro.Flush();

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Reautorización
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara no colgada (estado indeterminado)";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (AplicaServicioWindows)
                                {
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                    }
                                }
                                //else
                                //{

                                //    Eventos.ReportarCancelacionTurno( CaraID,  MensajeErrorLectura,  EstadoTurno);
                                //}
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                        }
                        break;
                    #endregion
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarAccion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void PeticionAutorizacion(object datos)
        {
            try
            {
                string[] data = (string[])datos;

                byte CaraTmp = Convert.ToByte(data[0]);
                int IdProducto = Convert.ToInt32(data[1]);
                int IdManguera = Convert.ToInt32(data[2]);
                string Lectura = data[3];

                if (AplicaServicioWindows)
                {
                    if (AutorizacionRequerida != null)
                    {
                        AutorizacionRequerida(CaraTmp, IdProducto, IdManguera, Lectura,"");
                    }
                }
                //else
                //{
                //    Eventos.RequerirAutorizacion( CaraTmp,  IdProducto,  IdManguera,  Lectura);
                //}

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo PeticionAutorizacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void InfoVentaParcial(object datos) //21/04/2012 DCF hilo para el envio de datos en Venta parciales 
        {

            try
            {
                string[] data = (string[])datos;

                byte CaraTmp = Convert.ToByte(data[0]);
                string strTotalVenta = data[1];
                string strVolumen = data[2];


                if (AplicaServicioWindows)
                {
                    if (VentaParcial != null)
                    {
                        VentaParcial(CaraTmp, strTotalVenta, strVolumen);
                    }
                }
                //else
                //{
                //    Eventos.InformarVentaParcial( CaraTmp,  strTotalVenta,  strVolumen);
                //}


            }
            catch (Exception Excepcion)
            {

                string[] data = (string[])datos;
                SWRegistro.WriteLine(DateTime.Now + "|*** Datos enviados en InformarVentaParcial " + "- CaraTmp: " + data[0] + " - strTotalVenta: " + data[1] + " - strVolumen: " + data[2]);
                SWRegistro.Flush(); // DCF 05/09/2012 Eventos.InformarVentaParcial 

                string MensajeExcepcion = "Excepcion en el InformarVentaParcial: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        //METODO PARA CONSTATAR GRADO
        public bool ConfirmacionGrado(int GradoAutorizado)
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Grado que requiere autorizacion 1er intento: " +
                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado);
                SWRegistro.Flush();

                Thread.Sleep(50);

                //Toma nuevamente el grado de la cara que se quiere autorizar
                if (!RecuperarEstadoExtendido())
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Surtidor no responde Estado Extendido 2. Reintentos");
                    SWRegistro.Flush();
                    return false;
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Grado que requiere autorizacion 2do intento: " +
                        EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado);
                    SWRegistro.Flush();

                    //Realiza Confirmación de grado a reautorizarse
                    if (GradoAutorizado == EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                        return true;
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado primera encuesta: " +
                            GradoAutorizado + " - Grado segunda encuesta: " + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado);
                        SWRegistro.Flush();
                        return false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ConfirmacionGrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        public void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;

                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                 EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].PrecioNivel1;
                             
                       

                //int Reintentos = 0;
                decimal VolumenCalculado = new decimal();

                //Obtiene los Valores Finales de la Venta (Metros cubicos despachados)
                ComandoCaras = ComandoSurtidor.Volumen_Fill;
                if (ProcesoEnvioComando(ComandoSurtidor.Volumen_Fill, false))
                {
                    //Si el grado que responde está dentro del la lista de grados
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >= EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura Final de Venta en el Grado: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                        SWRegistro.Flush();// escribir el grado que realizo la venta DCF 29-10-2013

                        //Obtiene la Lectura Final de la Venta
                       // EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoVenta;
                        TomarLecturas();

                        //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                        //if (EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado != EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                        //{
                            /*- WBC: Modificado el 10/07/2009 ---*/
                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado +
                            //    " - Grado que vendio: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                            //SWRegistro.Flush();

                            /*- WBC: Comentado el 10/07/2009 ---
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Lectura Inicial Tomada: " +
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta +
                                " - Lectura Inicial asumida: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta);
                            SWRegistro.Flush();

                            //Se asume lectura inicial como lectura final de la ultima venta
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta;
                             * ---------------------------------------------------------------------------------------------------------------------*/
                        //}

                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].Lectura;



                             
                             //Calcula el volumen despachado según lecturas Inicial y Final de venta 
                        VolumenCalculado =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta -
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta;
                       

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Lectura Inicial Obtenida: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta +
                            " - Lectura Inicial Grado: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta +
                            " - Lectura Final: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta +
                            " - Volumen Calculado: " + VolumenCalculado);
                        SWRegistro.Flush();


                        //Realiza comparación entre volumen calculado por lecturas y volumen obtenido por finalización de venta
                        // Tiene en cuenta si se reiniciaron las lecturas por secuencia normal del Totalizador del surtidor
                        if (VolumenCalculado >= 0)
                        {
                            //Si no se ha reiniciado el sistema, el valor de LecturaInicial es diferente de 0
                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta > 0)
                            {
                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta > 0)
                                {
                                    /*Se compara el valor de Volumen Calculado con el valor de Volumen Recibido.
                                     * La diferencia no debe exceder el (+/-) 1%.  
                                     * Se da mayor credibilidad al calculado por lecturas*/
                                    //if (EstructuraRedSurtidor[CaraEncuestada].Volumen < VolumenCalculado - Convert.ToDecimal(0.05) ||
                                    //    EstructuraRedSurtidor[CaraEncuestada].Volumen > VolumenCalculado + Convert.ToDecimal(0.05))
                                    //{
                                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Volumen Calculado: " + VolumenCalculado +
                                    //        " - Volumen Reportado: " + EstructuraRedSurtidor[CaraEncuestada].Volumen);
                                    //    SWRegistro.Flush();
                                    //    EstructuraRedSurtidor[CaraEncuestada].Volumen = VolumenCalculado;
                                    //    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                                    //        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;//DCF el precio de venta es 10000 pero se le envia al surtidor 1000, 
                                    //}
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Lectura Final de Venta en 0");
                                    SWRegistro.Flush();
                                }
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Lectura Inicial de Venta en 0");
                                SWRegistro.Flush();
                            }
                        }


                        

                        //Si se realizó una venta con valores de m3 y $ mayor que cero
                        if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                        {
                            //YEZID
                            //Aqui va el lanzamiento del evento de fin de venta
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                            ////Calcula la correspondencia entre el Volumen, el Precio y el Importe
                            //decimal TotalVentaCalculada = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                            //    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|MultiplicadorPrecioVenta = " + EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta); //Borra
                            //SWRegistro.Flush(); //Borrar solo para Prueba

                            ////Calcula la correspondencia entre el Volumen, el Precio y el Importe //DCF
                            decimal TotalVentaCalculada = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;//DCF el precio de venta es 10000 pero se le envia al surtidor 1000, 

                            decimal PorcentajeVenta = (TotalVentaCalculada * 2) / 100;   //Porcentaje del 5% //DCF 02-08-2011 corrección para Perú 
                            if (EstructuraRedSurtidor[CaraEncuestada].TotalVenta == 0 ||
                                //EstructuraRedSurtidor[CaraEncuestada].TotalVenta > TotalVentaCalculada + 100/ EstructuraRedSurtidor[CaraEncuestada].FactorImporte ||
                                //EstructuraRedSurtidor[CaraEncuestada].TotalVenta < TotalVentaCalculada - 100 / EstructuraRedSurtidor[CaraEncuestada].FactorImporte) //para colombia
                                EstructuraRedSurtidor[CaraEncuestada].TotalVenta > TotalVentaCalculada + (PorcentajeVenta) ||
                                EstructuraRedSurtidor[CaraEncuestada].TotalVenta < TotalVentaCalculada - (PorcentajeVenta)) // Para Peru 
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID +
                                    "|Inconsistencia|Valor recibido en : " + EstructuraRedSurtidor[CaraEncuestada].TotalVenta +
                                    " - Calculado: " + TotalVentaCalculada);
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = TotalVentaCalculada;

                            }

                            //se obtiene la presion de llenado
                            ComandoCaras = ComandoSurtidor.Post_Pressure_TGT;
                            ProcesoEnvioComando(ComandoSurtidor.Post_Pressure_TGT, false);

                            //Dispara evento al programa principal si la venta es diferente de 0
                            string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            string strPrecio = (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta).ToString("N3");
                            string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                            string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                            string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                            string bytProducto = Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].IdProducto);
                            int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].MangueraBD;
                            string PresionLLenado = Convert.ToString( EstructuraRedSurtidor[CaraEncuestada].PresionLlenado.ToString("N3"));
                            //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada


                            //Loguea evento Fin de Venta
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Finalizacion Venta. Importe: " + strTotalVenta +
                                " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                                " - Volumen: " + strVolumen + "- PresionLLenado: " + PresionLLenado  + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                            SWRegistro.Flush();

                           
                            string[] Args = { CaraID.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

                            //                      string Args = CaraEncuestada.ToString() + "|" + strTotalVenta.ToString() + "|" + strPrecio.ToString() + "|" + strLecturaFinalVenta.ToString() + "|" + strVolumen.ToString() + "|" + bytProducto.ToString() + "|" + IdManguera.ToString() + "|" + PresionLLenado.ToString() + "|" + strLecturaInicialVenta.ToString();

                            //Thread HiloFinalizacionVenta = new Thread(InformarFinalizacionVenta);
                            //HiloFinalizacionVenta.Start(Args);
                            ThreadPool.QueueUserWorkItem(new WaitCallback(InformarFinalizacionVenta), Args);

                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta;


                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                            if (AplicaServicioWindows)
                            {
                                if (VentaInterrumpidaEnCero != null)
                                {
                                    VentaInterrumpidaEnCero(CaraID);
                                }
                            }
                            //else
                            //{

                            //    Eventos.ReportarVentaEnCero( CaraID);
                            //}
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Reporta venta en CERO");
                            SWRegistro.Flush();
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Intento de Finalizacion de Venta en grado Inexistente (Grado " +
                            EstructuraRedSurtidor[CaraEncuestada].GradoVenta + ")");
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ProcesoFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public void InformarFinalizacionVenta(object args)
        {
            string[] Argumentos = (string[])args;
            //{ CaraEncuestada.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

            byte CaraEncuestadaFinVenta = Convert.ToByte(Argumentos[0]);
            string strTotalVenta = Argumentos[1];
            string strPrecio = Argumentos[2];
            string strLecturaFinalVenta = Argumentos[3];
            string strVolumen = Argumentos[4];
            string bytProducto = Convert.ToString(Argumentos[5]);
            int IdManguera = Convert.ToInt32(Argumentos[6]);
            string PresionLLenado = Argumentos[7];
            string strLecturaInicialVenta = Argumentos[8];


            if (AplicaServicioWindows)
            {
                if (VentaFinalizada != null)
                {
                    VentaFinalizada(CaraEncuestadaFinVenta, strTotalVenta, strPrecio, strLecturaFinalVenta, strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);
                }
            }
            //else
            //{
            //    byte bytProductoTerpel = Convert.ToByte(Argumentos[5]);
            //    Eventos.InformarFinalizacionVenta( CaraEncuestadaFinVenta,  strTotalVenta,  strPrecio,  strLecturaFinalVenta,
            //     strVolumen,  bytProductoTerpel,  IdManguera,  PresionLLenado,  strLecturaInicialVenta);
            //}


        }

        //OBTIENE LOS VALORES FINALES DE UNA VENTA
        public void RecuperarDatosFindeVenta()
        {
            try
            {
                //Calcula el LRC
                int LRCCalculado = CalcularLRC(TramaRx, 0, (TramaRx.Length - 3));
                int LRCObtenidoEnTama = TramaRx[(TramaRx.Length - 2)] & 0x0F;

                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTama == LRCCalculado)//Eco
                {
                    byte CaraqueResponde = Convert.ToByte((TramaRx[4] & (0x0F)) + 1);
                    if (CaraqueResponde == CaraEncuestada)
                    {

                        // Identificación del protocolo a utilizar  DCF_Extended
                        //Gilbarco Normal = 0
                        //if (CaraEncuestada == 0x03 || CaraEncuestada == 0x04 ||
                        //        CaraEncuestada == 0x05 || CaraEncuestada == 0x06)//SOlo el surtidor 2 cara 3 -4
                        if (EstructuraRedSurtidor[CaraEncuestada].Gilbarco_Extended)

                        //if (CaraEncuestada == 0x01 || CaraEncuestada == 0x02 ||
                        //    CaraEncuestada == 0x03 || CaraEncuestada == 0x04 ||
                        //    CaraEncuestada == 0x05 || CaraEncuestada == 0x06 ||
                        //    CaraEncuestada == 0x07 || CaraEncuestada == 0x08 ||
                        //    CaraEncuestada == 0x09 || CaraEncuestada == 0x10 ||
                        //    CaraEncuestada == 0x11 || CaraEncuestada == 0x12)// para los 6 surtidores 
                        {
                            ////Se obtiene el Precio con que se realizo la venta
                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                ObtenerValor(12, 17) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;


                            //Se obtiene el Volumen despachado
                            EstructuraRedSurtidor[CaraEncuestada].Volumen =
                                ObtenerValor(19, 26) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                            //Se obtiene el Dinero despachado
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta =
                                ObtenerValor(28, 35) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;

                            //Se Optiene el grado por donde se despacho
                            EstructuraRedSurtidor[CaraEncuestada].GradoVenta = Convert.ToByte(0x0F & TramaRx[9]);
                            if (EstructuraRedSurtidor[CaraEncuestada].GradoVenta != EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado Autorizado_Extended: " +
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " difiere de grado que reporta fin de venta: " +
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                                SWRegistro.Flush();
                            }
                        }
                        else
                        {
                            ////Se obtiene el Precio con que se realizo la venta
                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                ObtenerValor(12, 15) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;


                            //Se obtiene el Volumen despachado
                            EstructuraRedSurtidor[CaraEncuestada].Volumen =
                                ObtenerValor(17, 22) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                            //Se obtiene el Dinero despachado
                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta =
                                ObtenerValor(24, 29) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;

                            //Se Optiene el grado por donde se despacho
                            EstructuraRedSurtidor[CaraEncuestada].GradoVenta = Convert.ToByte(0x0F & TramaRx[9]);
                            if (EstructuraRedSurtidor[CaraEncuestada].GradoVenta != EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado Autorizado: " +
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " difiere de grado que reporta fin de venta: " +
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                                SWRegistro.Flush();
                            }

                        }

                        //No hubo error por fallas en datos
                        InconsistenciaDatosRx = false;
                    }
                    else
                    {
                        InconsistenciaDatosRx = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Comando " + ComandoCaras
                            + " no corresponde a Cara que responde: " + CaraqueResponde);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    InconsistenciaDatosRx = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras + " responde LRC Errado. LRC Obtenido: " +
                        LRCObtenidoEnTama + " - LRC Calculado: " + LRCCalculado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarDatosFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        public void LecturaAperturaCierre()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();
                TomarLecturas();

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                ////Cambia el precio si es apertura de turno
                //if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                //    SWRegistro.Flush();
                //    CambiarPrecios(EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count * 2);
                //}

                int i;
                for (i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {

                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio)
                        ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD) + "|" +
                            Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura) + "|" +
                            Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1));
                    else
                        ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD) + "|" +
                            Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura) + "|0");


                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Finales/Iniciales de turno. Manguera " +
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD + " - Lectura " +
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura);
                    SWRegistro.Flush();
                }

                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                ArrayLecturas.CopyTo(LecturasEnvio);

                //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                {

                    if (AplicaServicioWindows)
                    {
                        if (LecturaTurnoCerrado != null)
                        {
                            LecturaTurnoCerrado(LecturasEnvio);


                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Finales de turno");
                            SWRegistro.Flush();
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                        }
                    }
                    //else
                    //{

                    //    Eventos.InformarLecturaFinalTurno( LecturasEnvio);
                    //}
                   
                }
                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    if (AplicaServicioWindows)
                    {
                        if (LecturaTurnoAbierto != null)
                        {
                            LecturaTurnoAbierto(LecturasEnvio);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Iniciales de turno");
                            SWRegistro.Flush();
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                        }
                    }
                    //else
                    //{

                    //    Eventos.InformarLecturaInicialTurno( LecturasEnvio);
                    //}
                 
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA COMANDO DE TOMA DE LECTURAS Y LANZA ENVENTO PARA REPORTAR LECTURAS AL SERVICIO WINDOWS
        public void TomarLecturas()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;

                //Se resetea la lectura de cada grado de la cara
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura = 0;

                //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 2 " + CaraID);
                //SWRegistro.Flush();

                //Realiza hasta tres reintentos de toma de lecturas si hubo error en la obtención
                do
                {
                    Reintentos += 1;
                    ComandoCaras = ComandoSurtidor.Volumen_Total;

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Inicio| ********* 3 " + CaraID);
                    //SWRegistro.Flush();

                    if (!ProcesoEnvioComando(ComandoSurtidor.Volumen_Total, false))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Surtidor no respondio a comando Toma de Lectura");
                        SWRegistro.Flush();
                    }
                    else
                        break;

                } while (Reintentos <= 3);
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarLecturas: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EGV:OBTIENE LOS VALORES DE LAS LECTURAS CUANDO LA CARA ESTA INACTIVA
        public System.Collections.ArrayList TomarLecturaActivacionCara()
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                ArrayLecturas = new System.Collections.ArrayList();

                //Se resetea la lectura de cada grado de la cara
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura = 0;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales, false))
                    {
                        //Si el proceso no fue exitoso, la función devuelve False
                        return new System.Collections.ArrayList();
                    }
                } while (Reintentos <= 3 && ExistenLecturasEnCero(CaraEncuestada));

                //Se verifica si existen lecturas en cero para cada grado
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura == 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Manguera " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD + "|Lectura recibida en CERO - ACTIVACION DE CARA");
                        SWRegistro.Flush();
                    }

                    ArrayLecturas.Add(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD + "|" + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].Lectura);
                }

                //Si el proceso de toma de lecturas fue exitoso, devuelve el arreglo de lecturas
                return ArrayLecturas;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo TomarLecturaActivacionCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return new System.Collections.ArrayList();
            }
        }

        //Verifica que existan lecturas en cero en una cara en particular
        public bool ExistenLecturasEnCero(byte Cara)
        {
            try
            {
                Boolean Existen = false;
                for (int i = 0; i <= EstructuraRedSurtidor[Cara].ListaGrados.Count - 1; i++)
                {
                    if (EstructuraRedSurtidor[Cara].ListaGrados[i].Lectura == 0)
                    {
                        Existen = true;
                    }
                }

                return Existen;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ExistenLecturasEnCero: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return true;
            }
        }

        //OBTIENE LOS VALORES DE LAS LECTURAS (TOTALIZADORES)
        public void RecuperarTotalizadores() // modificar para obtener el totalizador   Value [0] = 17292;   Value [1] = 55040
        {
            try
            {

                 Valor = Obtener_Valor_Float(hexValue1 + hexValue2);
                
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[0].Lectura = (Convert.ToDecimal(Valor));

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarTotalizadores 2: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //OBTIENE EL VALOR EN PESOS DE LA VENTA EN CURSO Y CALCULA A PARTIR DE ESTE Y EL PRECIO EL VALOR DE VOLUMEN
        public void RecuperarParcialesdeVenta()
        {
            try
            {


                //int data1 = values[1];
                //int data2 = values[0];
                //// Convert integer Values[] as a hex in a string variable
                //string hexValue1 = data1.ToString("X").PadRight(4, '0');
                //string hexValue2 = data2.ToString("X").PadRight(4, '0');
                //// Convert the hex string back to the number
                ////int decAgain = int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);

                 Valor = Obtener_Valor_Float(hexValue1 + hexValue2);

                EstructuraRedSurtidor[CaraEncuestada].Volumen = (Convert.ToDecimal(Valor));

                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].Volumen * EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarParcialesdeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void RecuperarPresion_TGT()
        {
            try
            {      


                 Valor = Obtener_Valor_Float(hexValue1 + hexValue2);

                EstructuraRedSurtidor[CaraEncuestada].PresionLlenado = (Convert.ToDecimal(Valor));

                

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarParcialesdeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        //EVALUA SI LA CARA ENVIO CONFIRMACION PARA ENVIO DE DATOS
        public void ConfirmacionEnvioDatos()
        {
            try
            {
                //Almacena el Nibble Respuesta
                byte Respuesta = Convert.ToByte(TramaRx[0] & (0xF0));//Eco

                InconsistenciaDatosRx = false;

                //Se evalua si el Surtidor esta preparado para recibir los datos
                if (Respuesta == 0xD0)
                {
                    if (Convert.ToByte(TramaRx[0] & (0x0F)) != CaraEncuestada)//Eco
                    {
                        InconsistenciaDatosRx = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras
                            + " no corresponde a Cara que responde: " + Convert.ToByte(TramaRx[0] & (0x0F)));
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    InconsistenciaDatosRx = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras +
                        " Respuesta erronea recibida: " + TramaRx[0]);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ConfirmacionEnvioDatos: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //CAMBIA LOS PRECIOS DE CADA PRODUCTO DE CADA CARA
        public bool CambiarPrecios(int NumeroDePreciosACambiar)
        {
            try
            {
                //Variable que indica qué precio cambiar
                bool PrecioNivel1 = new bool();
                int Reintentos = 0;

                //Recupera todos los valores de precios de la cara encuestada
                //ProcesoEnvioComando(ComandoSurtidor.Totales, false);

                //Si hay cambio de precio pendiente, lo aplica
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    EstructuraRedSurtidor[CaraEncuestada].GradoCara = i;
                    //Compara el Nivel 1 del precio del grado
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 !=
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio de precio Nivel 1 Grado: " +
                            EstructuraRedSurtidor[CaraEncuestada].GradoCara +
                            " - Precio actual: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                            " - Precio nuevo: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1);
                        SWRegistro.Flush();
                        PrecioNivel1 = true;
                        Reintentos = 0;
                        do
                        {
                            if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                            {
                                Thread.Sleep(20);
                                ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                                //EnviarComando();
                                if (EsTCPIP)
                                {
                                    EnviarComando_TCPIP();
                                    RecibirInformacion_TCPIP();
                                }

                                else
                                    EnviarComando();

                                ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID +
                                "|Error|No acepto comando Envio de datos para cambio de precio");
                                SWRegistro.Flush();
                            }
                            Reintentos += 1;

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 ==
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1)
                            {
                                NumeroDePreciosACambiar -= 1;
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio = true;
                                break;
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                                    " - Precio Requerido: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 +
                                    " - Reintentos: " + Reintentos);
                                SWRegistro.Flush();

                                //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                int Manguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD;
                                double Precio = Convert.ToDouble(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1);
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio = false;

                                if (AplicaServicioWindows)
                                {
                                    if (CambioPrecioFallido != null)
                                    {
                                        CambioPrecioFallido(Manguera, Precio);
                                    }
                                }
                                //else
                                //{
                                //    Eventos.ReportarCambioPrecioFallido( Manguera,  Precio);
                                //}
                            }
                        } while (Reintentos <= 3);
                    }
                    else
                    {
                        NumeroDePreciosACambiar -= 1;
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio = false;
                    }

                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 !=
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2)
                    {
                        PrecioNivel1 = false;
                        Reintentos = 0;
                        do
                        {
                            if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                            {
                                ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                                //EnviarComando();
                                if (EsTCPIP)
                                {
                                    EnviarComando_TCPIP();
                                    RecibirInformacion_TCPIP();
                                }

                                else
                                    EnviarComando();

                                ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para cambio de precio");
                                SWRegistro.Flush();
                            }
                            Reintentos += 1;

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2 ==
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2)
                            {
                                NumeroDePreciosACambiar -= 1;
                                break;
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 2: Precio del Surtidor: " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2 +
                                    " - Precio Requerido: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 +
                                    " - Reintentos: " + Reintentos);
                                SWRegistro.Flush();
                            }

                        } while (Reintentos <= 3);
                    }
                    else
                        NumeroDePreciosACambiar -= 1;
                }
                //Si pudo cambiar ambos precios de todos los grados, se devuelve un cambio de precio exitoso
                if (NumeroDePreciosACambiar == 0)
                    return true;
                else
                    return false;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPrecio: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //CAMBIA LOS PRECIOS DE CADA PRODUCTO DE CADA CARA
        public bool CambiarPreciosEnGrado(Grados grado)
        {
            bool CambioPrecios = false;

            try
            {
                //Variable que indica qué precio cambiar
                bool PrecioNivel1 = new bool();
                int Reintentos = 0;

                //Si hay cambio de precio pendiente, lo aplica
                for (int i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].NoGrado == grado.NoGrado)
                    {
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = i;

                        //Compara el Nivel 1 del precio del grado
                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 !=
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1)
                        {
                            PrecioNivel1 = true;
                            Reintentos = 0;
                            do
                            {
                                if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                                {
                                    Thread.Sleep(20);

                                    // Cambio de precio / MultiplicadorPrecioVenta para el cambio de producto 24-03-2012
                                    //control del precio en el cambio de producto
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 /
                                        EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;

                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 /
                                        EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;


                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio de Precio por Producto: Precio PrecioNivel1 = " +
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1
                                        + " - Cambio de Precio PrecioNivel2 = " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2);
                                    SWRegistro.Flush();

                                    ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                                    //EnviarComandComandoSurtidor.CambiarPrecioo();
                                    if (EsTCPIP)
                                    {
                                        EnviarComando_TCPIP();

                                        RecibirInformacion_TCPIP();
                                    }

                                    else
                                        EnviarComando();



                                    ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para cambio de precio");
                                    SWRegistro.Flush();
                                }
                                Reintentos += 1;

                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 ==
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1)
                                {
                                    CambioPrecios = true;
                                    break;
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                                        " - Precio Requerido: " +
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1 +
                                        " - Reintentos: " + Reintentos);
                                    SWRegistro.Flush();

                                    //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                    int Manguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].MangueraBD;
                                    double Precio = Convert.ToDouble(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1);

                                    if (AplicaServicioWindows)
                                    {
                                        if (CambioPrecioFallido != null)
                                        {
                                            CambioPrecioFallido(Manguera, Precio);
                                        }
                                    }
                                    //else
                                    //{
                                    //    Eventos.ReportarCambioPrecioFallido( Manguera,  Precio);
                                    //}
                                }
                            } while (Reintentos <= 3);
                        }
                        else
                            CambioPrecios = true;

                        if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 !=
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2)
                        {
                            PrecioNivel1 = false;
                            Reintentos = 0;
                            do
                            {
                                if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                                {
                                    ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                                    //EnviarComando();
                                    if (EsTCPIP)
                                    {
                                        EnviarComando_TCPIP();

                                        RecibirInformacion_TCPIP();
                                    }

                                    else
                                        EnviarComando();



                                    ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para cambio de precio");
                                    SWRegistro.Flush();
                                }
                                Reintentos += 1;

                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2 ==
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2)
                                {
                                    CambioPrecios = true;
                                    break;
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 2: Precio del Surtidor: " +
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel2 +
                                        " - Precio Requerido: " +
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel2 +
                                        " - Reintentos: " + Reintentos);
                                    SWRegistro.Flush();
                                }

                            } while (Reintentos <= 3);
                        }
                        else
                            CambioPrecios = true;
                    }
                }

                //Si pudo cambiar ambos precios en el grado, se devuelve un cambio de precio exitoso
                if (CambioPrecios)
                    return true;
                else
                    return false;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPreciosEnGrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        public void CambiarPrecioVenta()
        {
            decimal TemporalPrecioNivel1 = new decimal();
            EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado;

            //Si el Precio con que se requiere realizar la venta no coincide con el precio del surtidor, se cambia el precio del surtidor
            if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta ==
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1)
            {
                //Se almacena el Precio Nivel 1 de Base de Datos del grado con que se está trabajando
                TemporalPrecioNivel1 =
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1;

                //Se configura el nuevo Precio Nivel 1
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                //Se activa la bandera que indica que el precio a cambiarse corresponde al Nivel 1
                bool PrecioNivel1 = true;
                int Reintentos = 0;
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                    {
                        Thread.Sleep(20);
                        ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                        //EnviarComando();
                        if (EsTCPIP)
                        {
                            EnviarComando_TCPIP();

                            RecibirInformacion_TCPIP();
                        }

                        else
                            EnviarComando();



                        ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para cambio de precio de venta");
                        SWRegistro.Flush();
                    }
                    Reintentos += 1;

                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 ==
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1)
                    {
                        //Se restaura el valor real del Precio Nivel 1, para, luego en el fin de la venta restaurar dicho en valor en surtidor
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                            TemporalPrecioNivel1;
                        break;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 +
                            " - Precio Requerido: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 +
                            " - Reintentos: " + Reintentos);
                        SWRegistro.Flush();
                    }

                } while (Reintentos <= 3);

                //Si no logró cambiar el precio en surtidor, se restaura el valor real del Precio Nivel 1
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                    TemporalPrecioNivel1;
            }
        }

        private bool CambiarPrecioVenta(decimal precio_cliente)
        {
            decimal TemporalPrecioNivel1 = new decimal();
            EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado;

            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = precio_cliente;

            //Si el Precio con que se requiere realizar la venta no coincide con el precio del surtidor, se cambia el precio del surtidor
            if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta !=
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1)
            {
                //Se almacena el Precio Nivel 1 de Base de Datos del grado con que se está trabajando
                TemporalPrecioNivel1 =
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1;

                //Se configura el nuevo Precio Nivel 1 
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                //Se activa la bandera que indica que el precio a cambiarse corresponde al Nivel 1
                bool PrecioNivel1 = true;
                int Reintentos = 0;
                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                    {

                        Thread.Sleep(20);
                        ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                        //EnviarComando();
                        if (EsTCPIP)
                        {
                            EnviarComando_TCPIP();
                            RecibirInformacion_TCPIP();
                        }

                        else
                            EnviarComando();
                       
                        Thread.Sleep(20);
                        ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                        EnviarComando();
                        ProcesoEnvioComando(ComandoSurtidor.Totales, false);
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para cambio de precio de venta");
                        SWRegistro.Flush();

                    }
                    Reintentos += 1;

                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 ==
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1)
                    {
                        //Se restaura el valor real del Precio Nivel 1, para, luego en el fin de la venta restaurar dicho en valor en surtidor
                        //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                        //    TemporalPrecioNivel1;

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|cambio de Precio Nivel 1 Correcto: Precio del Surtidor =  " +
                          EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1);

                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].CambioPrecioVentaActivo = true;

                        break;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 +
                            " - Precio Requerido: " +
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 +
                            " - Reintentos: " + Reintentos);
                        SWRegistro.Flush();


                    }

                } while (Reintentos <= 3);


            }

            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 ==
                      EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1
               )
            {
                //Si no logró cambiar el precio en surtidor, se restaura el valor real del Precio Nivel 1
                if (TemporalPrecioNivel1 != 0)
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 = TemporalPrecioNivel1;

                return true;
            }
            else
            {
                //Si no logró cambiar el precio en surtidor, se restaura el valor real del Precio Nivel 1
                //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                //    TemporalPrecioNivel1;

                return false;
            }

        }

        //REALIZA PROCESO PARA PREDETERMINAR UNA VENTA (POR METROS CUBICOS O POR DINERO)
        public bool Predeterminar()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
                {
                    //Si se va a predeterminar por importe y el valor a predeterminar NO supera las 6 cifras
                    if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte &&
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * EstructuraRedSurtidor[CaraEncuestada].FactorImporte <= 999999)
                    {
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaDinero, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Importe Predeterminado: " +
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();
                    }
                    //Si se va a predetrminar por importe y el valor a predeterminar supera las 6 cifras
                    else if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte &&
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * EstructuraRedSurtidor[CaraEncuestada].FactorImporte > 999999)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Importe a predeterminar: " +
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        //Convierte la cifra de Importe a Volumen
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado =
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1;

                        //Arma trama para predeterminar por volumen
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Volumen Predeterminado (importe convertido): " +
                           EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();

                        EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false; //DCF

                    }
                    else// if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                    {
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Volumen Predeterminado: " +
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();
                    }

                    //EnviarComando();
                    if (EsTCPIP)
                    {
                        EnviarComando_TCPIP();
                        RecibirInformacion_TCPIP(); //Debe leer despues de cada envio. DCF 20-10-2014
                    }

                    else
                        EnviarComando();
                    return true;
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para Predeterminar");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo Predeterminar: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        public bool RecuperarEstadoExtendido()
        {
            if (ProcesoEnvioComando(ComandoSurtidor.EnviarDatos, false))
            {
                if (ProcesoEnvioComando(ComandoSurtidor.EstadoExtendido, false))
                    return true;
                else
                    return false;
            }
            else
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No acepto comando Envio de datos para Estado Extendido");
                SWRegistro.Flush();
                return false;
            }
        }
        //#endregion

        public void VerifySizeFile()
        {
            try
            {
                //FileInfo 
                //FileInfo FileInf = new FileInfo(ArchivoTramas);
                FileInfo FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-FillPos_IMW-Sucesos(" + DireccionIP + ").txt";
                    SWRegistro = File.AppendText(Archivo);
                }


               
                
                FileInf = new FileInfo(ArchivoTramas);

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-FillPos_IMW-Tramas.(" + DireccionIP + ").txt";
               
                    SWTramas = File.AppendText(ArchivoTramas);
                }




            }
            catch (Exception ex)
            {
                try
                {
                    string MensajeExcepcion = "Excepcion en VerifySizeFile: " + ex;
                    SWRegistro.WriteLine(DateTime.Now + "|" + "|Excepcion|" + MensajeExcepcion);
                    SWRegistro.Flush();
                }
                catch (Exception)
                {

                }

            }

        }


        #region METODOS AUXILIARES

        //CALCULA EL CARACTER DE REDUNDANCIA CICLICA
        public int CalcularLRC(byte[] Trama, int Inicio, int Fin)
        {
            try
            {
                int LRC = new int();
                for (int i = Inicio; i <= Fin; i++)
                    LRC += (Trama[i] & 0x0F);
                LRC = ((LRC ^ 0x0F) + 1) & 0x0F;
                return LRC;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CalcularLRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 1;
            }
        }

        public decimal ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal Valor = new decimal();
                for (int i = PosicionInicial; i <= PosicionFinal; i++)
                    Valor += Convert.ToDecimal((TramaRx[i] & 0x0F) * Math.Pow(10, i - PosicionInicial));
                return Valor;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ObtenerValor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return 1;
            }
        }
        #endregion

        #region EVENTOS DE LA CLASE

        public void Evento_InactivarCaraCambioTarjeta( byte Cara,  string Puerto)
        {
            try
            {
                //CaraID = EstructuraRedSurtidor[Cara].CaraBD; //DCF Alias

                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    EstructuraRedSurtidor[CaraTmp].InactivarCara = true;
                    EstructuraRedSurtidor[CaraTmp].PuertoParaImprimir = Puerto;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Comando para Inactivar");
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Eventos_InactivarCaraCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //EGV: EVENTO PARA SOLICITAR ACTIVACION DE CARA
        public void Evento_FinalizarCambioTarjeta(byte Cara)
        {
            try
            {
                //CaraID = EstructuraRedSurtidor[Cara].CaraBD; //DCF Alias
                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    EstructuraRedSurtidor[CaraTmp].ActivarCara = true;
                    EstructuraRedSurtidor[CaraTmp].Activa = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Activada");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_FinalizarCambioTarjeta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        // Thread HiloVenta  13/04/2012
        public void Evento_VentaAutorizada( byte Cara,  string Precio,  string ValorProgramado,  byte TipoProgramacion,  string Placa,  int MangueraProgramada,  bool EsVentaGerenciada,string guid)
        {
            try
            {

                byte CaraTmp;

                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada + " Precio: " +Precio);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[CaraTmp].AutorizarCara = true;

                    ///////////////////////////
                    //Sólo para pruebas
                    //TipoProgramacion = 1;
                    //EstructuraRedSurtidor[Cara].ValorPredeterminado = Convert.ToDecimal(0.1);
                    //////////////////////////



                    //Valor a programar
                    EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    EstructuraRedSurtidor[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    EstructuraRedSurtidor[CaraTmp].MangueraProgramada = Convert.ToInt16(MangueraProgramada);

                    EstructuraRedSurtidor[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                        else
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                    }

                }


                //  para prueba de ON Hilo 
                else
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|La Cara está Fuera de la red de Surtidores..");
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }


        }


        public void Evento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();



                //Diccionario de los precios manejados por cada manguera
                Dictionary<int, Grados> Grados = new Dictionary<int, Grados>();

                //Ciclo que arma Diccionario de Grados
                foreach (string sPreciosManguera in Precios)
                {
                    //Objeto Grado para añadir al Diccionario
                    Grados PrecioGrado = new Grados();

                    string[] vPreciosGrado = sPreciosManguera.Split('|');
                    PrecioGrado.IdProducto = Convert.ToByte(vPreciosGrado[0]);
                    PrecioGrado.PrecioNivel1 = Convert.ToDecimal(vPreciosGrado[1]);
                    PrecioGrado.PrecioNivel2 = Convert.ToDecimal(vPreciosGrado[2]);
                    PrecioGrado.MangueraBD = Convert.ToInt16(vPreciosGrado[3]);

                    //Si La Manguera no existe dentro del diccionario, lo añade
                    if (!Grados.ContainsKey(PrecioGrado.MangueraBD))
                        Grados.Add(PrecioGrado.MangueraBD, PrecioGrado);
                    else
                    {
                        Grados[PrecioGrado.MangueraBD].IdProducto = PrecioGrado.IdProducto;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel1 = PrecioGrado.PrecioNivel1;
                        Grados[PrecioGrado.MangueraBD].PrecioNivel2 = PrecioGrado.PrecioNivel2;
                    }
                }

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);
                    

                    
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores


                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + " CaraTmp = " + CaraTmp);//Borrar
                        SWRegistro.Flush();


                        //CaraTmp = 12; //Arrglar  con JD 

                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        {
                            if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta = 1;


                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "| TomarLecturaAperturaTurno = " + EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno);//Borrar
                                SWRegistro.Flush();
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta + " - PrecioNivel1: " +
                                Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);
                                SWRegistro.Flush();// Borrar solo para Inspección

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                   (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                   EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 2011.03.15-1705
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores Cara Impar. Evento: Evento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + " CaraLectura = " + CaraLectura);//Borrar
                        SWRegistro.Flush();

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF ??

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + " CaraTmp = " + CaraTmp);//Borrar
                        SWRegistro.Flush();


                       // CaraTmp = 4; //Arrglar  con JD 

                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + " CaraTmp = " + CaraTmp);//Borrar
                        //SWRegistro.Flush();

                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)
                                EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta = 1;

                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta + " - PrecioNivel1: " +
                                Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);
                                SWRegistro.Flush();// Borrar solo para Inspección

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                   (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                   EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 2011.03.15-1705

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2) /
                                    EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel2011.03.15-1705
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores Cara Par. Evento: Evento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_TurnoCerrado( string Surtidores,  string PuertoTerminal)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoCerrado). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte CaraLectura;
                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Evento|Fuera de red de surtidores. Evento: Evento_TurnoCerrado");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                        //if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: Evento_TurnoCerrado");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Evento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        ////Evento que manda a cambiar el producto y su respectivo precio en las mangueras
        //public void Eventos_ProgramarCambioPrecioKardex( SharedEventsFuelStation.ColMangueras mangueras)
        //{
        //    try
        //    {
        //        //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
        //        foreach (SharedEventsFuelStation.Manguera OManguera in mangueras)
        //        {
        //            foreach (RedSurtidor ORedSurtidor in EstructuraRedSurtidor.Values)
        //            {
        //                foreach (Grados OGrado in ORedSurtidor.ListaGrados)
        //                {
        //                    if (OGrado.MangueraBD == OManguera.idManguera)
        //                    {
        //                        ORedSurtidor.CambiarProductoAMangueras = true;
        //                        OGrado.IdProductoACambiar = OManguera.IdProductoActivo;
        //                        OGrado.PrecioNivel1 = Convert.ToDecimal(OManguera.Precio);
        //                        OGrado.PrecioNivel2 = Convert.ToDecimal(OManguera.Precio);
        //                        OGrado.CambiarProducto = true;
        //                        SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD + "|Manguera: " + OGrado.MangueraBD +
        //                            " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
        //                        SWRegistro.Flush();
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepcion en el Evento Eventos_ProgramarCambioPrecioKardex: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}


        //public void Eventos_ProgramarCambioPrecioKardexServicioTerpel(Gasolutions.FabricaProtocoloServicio.ColMangueras mangueras)
        //{
        //    try
        //    {


        //        //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
        //        foreach (Gasolutions.FabricaProtocoloServicio.Manguera OManguera in mangueras)
        //        {
        //            foreach (RedSurtidor ORedSurtidor in EstructuraRedSurtidor.Values)
        //            {
        //                foreach (Grados OGrado in ORedSurtidor.ListaGrados)
        //                { 
        //                    if (OGrado.MangueraBD == OManguera.idManguera)
        //                    {
        //                        ORedSurtidor.CambiarProductoAMangueras = true;
        //                        OGrado.IdProductoACambiar = OManguera.IdProductoActivo;
        //                        OGrado.PrecioNivel1 = Convert.ToDecimal(OManguera.Precio);
        //                        OGrado.PrecioNivel2 = Convert.ToDecimal(OManguera.Precio);
        //                        OGrado.CambiarProducto = true;
        //                        SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD + "|Manguera: " + OGrado.MangueraBD +
        //                            " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
        //                        SWRegistro.Flush();
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepcion en el Evento Eventos_ProgramarCambioPrecioKardex: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}


        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }

       public void Evento_CerrarProtocolo()
        {
            try
            {

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                this.CondicionCiclo = false;

                if (PuertoCom.IsOpen)
                {
                    PuertoCom.Close();

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|PuertoCom Close");
                    SWRegistro.Flush();
                }



            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Eventos_CerrarProtocolo " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
            }
        }
        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)//Realizado por el remplazo del shared event, usa una interfaz en el proyecto Fabrica Protocolo
        {
            try
            {

                //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                //foreach (Gasolutions.FabricaProtocoloServicio.Manguera OManguera in mangueras)
                //{
                for (int i = 1; i <= mangueras.Count; i++)
                {
                    Manguera OManguera = mangueras.get_Item(i);

                    foreach (RedSurtidor ORedSurtidor in EstructuraRedSurtidor.Values)
                    {
                        foreach (Grados OGrado in ORedSurtidor.ListaGrados)
                        {
                            if (OGrado.MangueraBD == OManguera.idManguera)
                            {
                                ORedSurtidor.CambiarProductoAMangueras = true;
                                OGrado.IdProductoACambiar = OManguera.IdProductoActivo;
                                OGrado.PrecioNivel1 = Convert.ToDecimal(OManguera.Precio);
                                OGrado.PrecioNivel2 = Convert.ToDecimal(OManguera.Precio);
                                OGrado.CambiarProducto = true;
                                SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD + "|Manguera: " + OGrado.MangueraBD +
                                    " - Producto: " + OGrado.IdProducto + "Precio Nivel 1 = " + OGrado.PrecioNivel1 +
                                     " - Precio Nivel 2 = " + OGrado.PrecioNivel2 + " - Solicitud de cambio de producto");
                                SWRegistro.Flush();
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento Eventos_ProgramarCambioPrecioKardexServicioTerpel:" + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


    





        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {
            try
            {
                CaraTmp = ConvertirCaraBD(Cara); //DCF
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    EstructuraRedSurtidor[CaraTmp].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibiendo evento de Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento Evento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }


  

        public void Evento_CancelarVenta(byte Cara)
        {

        } 

        #endregion
    }
}



        #endregion;