


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
using System.Net;


//using gasolutions.Factory;
namespace POSstation.Protocolos
{

    //public class H2P_Safe : iProtocolo  
     public class PumpControl : iProtocolo
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
        bool Error_ConexionTCP = false;  //DCF 30/08/2017
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
        bool CondicionCiclo2 = true;
        bool EncuentaFinalizada = false;


        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        byte CaraID;//DCF Alias 

        public enum ComandoSurtidor
        {
            Estado,
            SW_Normal,
            Habilitar_Boquillas,
            Enable,
            Autorizar,
            Fin_Despacho,
            Stop,
            Hold_Distribution,
            Resume,
            Borra_Restablecer,
            PrecioDespacho,

           
            TotalDespacho,
            ParcialDespacho,
            Totales,

            //Trama para transmision de datos a la Cara, enviados despues del comando 0x02 (EnviarDatos)            
            CambiarPrecio,
            Predeterminar,
            PredeterminarVentaDinero,
            PredeterminarVentaVolumen,
          
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
        string Puerto;

        TcpClient ClienteGilbarco;


        NetworkStream Stream;

        //byte[] TramaRxTemporal = new byte[250];
        int BytesRecibidos = 0;

        bool CRC_RX_ = false;


     public PumpControl(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                this.Puerto = Puerto;

                AplicaServicioWindows = true;
                //this.AplicaTramas = AplicaTramas;                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion

                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Odd;
                    PuertoCom.ReadBufferSize = 4096;
                    PuertoCom.WriteBufferSize = 4096;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception Excepcion)
                    {
                        string MensajeExcepcion = "No se pudo abrir puerto de comunicacion: " + Excepcion;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                        throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }


                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;


                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 05.09.2016-1522");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 16.09.2016-1258");
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 03.10.2016-1911"); //DCF 04_10_16 18:26
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 05.10.2016-1303");//Para controlar el envio en el Evento|Informar Lectura Inicial de Venta  --- DCF 05-10-2016 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 11.10.2016-1950");// 11_10_2016 DCF
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 12.10.2016-2053");//12-10-2016
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 13.10.2016-1514");//13-10-2016 DCF
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 14.10.2016-1145");//DCF 14-10-2016
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 15.10.2016-0000"); // 14-10-2016 DCF Prede
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 20.01.2017- 1551");//20/01/2017 DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 24.05.2017- 1617");//DCF 24/05/2017 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 17.07.2017- 1813 ");// DCF 17/07/2017
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 30.08.2017- 1245 ");//DCF 30/08/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 14.12.2017- 1007");// DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 08.03.2018- 1659"); //DCF Archivos .txt 08/03/2018  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 24.08.2018- 1700");//DCF 24_08_2014

                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Numero de caras: " + EstructuraRedSurtidor.Count);
                SWRegistro.Flush();

                SWRegistro.Flush();

                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " Grado Venta Parcial: " + oCara.GradoMangueraVentaParcial + " Eco: " + TramaEco.ToString() + " AplicaCambioPrecioCliente: " + oCara.AplicaCambioPrecioCliente);
                }
                SWRegistro.Flush();

                TramaEco = Eco;



                ThreadPool.QueueUserWorkItem(CicloCara); //--Modificado 2012.04.09-0934
                // HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                //HiloCicloCaras.Start();
            }
            catch (ThreadAbortException ex)
            {

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase Gilbarco: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


     public PumpControl(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
     
        {
            try
            {
                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);



                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;

                AplicaServicioWindows = true;
                TramaEco = Eco;

                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteGilbarco.GetStream();

                    }

                    catch (Exception e)
                    {
                        string MensajeExcepcion = "No se pudo Crear la conexión con el Server: " + DireccionIP + ": " + Puerto + e;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                    }


                }
                else if (!PuertoCom.IsOpen)
                {

                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Odd;
                    PuertoCom.ReadBufferSize = 4096;
                    PuertoCom.WriteBufferSize = 4096;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception Excepcion)
                    {
                        string MensajeExcepcion = "No se pudo abrir puerto de comunicación _ Configuración TCPIP recibida: " + Excepcion;
                        SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                        SWRegistro.Flush();
                        throw Excepcion; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }




                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 16.09.2016-1258");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 03.10.2016-1911"); //DCF 04_10_16 18:26
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 05.10.2016-1303");//Para controlar el envio en el Evento|Informar Lectura Inicial de Venta  --- DCF 05-10-2016 
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 11.10.2016-1950");// 11_10_2016 DCF
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 12.10.2016-1812");//12-10-2016
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 13.10.2016-1514");//13-10-2016 DCF
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 14.10.2016-1145");//DCF 14-10-2016
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 15.10.2016-0000"); // 14-10-2016 DCF Prede
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 20.01.2017- 1551");//20/01/2017 DCF
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 24.05.2017- 1617");//DCF 24/05/2017 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 17.07.2017- 1813 * ");// DCF 17/07/2017
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 30.08.2017- 1245 *");//DCF 30/08/2017
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 14.12.2017- 1007");// DCF11/12/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 08.03.2018- 1659"); //DCF Archivos .txt 08/03/2018  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo H2P_Safe. Modificado 24.08.2018- 1700");//DCF 24_08_2014

               

                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Numero de caras: " + EstructuraRedSurtidor.Count);
                SWRegistro.Flush();


                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " Grado Venta Parcial: " + oCara.GradoMangueraVentaParcial + " Eco: " + TramaEco.ToString() + " AplicaCambioPrecioCliente: " + oCara.AplicaCambioPrecioCliente);
                }
                SWRegistro.Flush();


                ThreadPool.QueueUserWorkItem(CicloCara);



            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase Gilbarco: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF 2011-05-14
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
        public void CicloCara(object e)
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //para loguear los factores
                foreach (RedSurtidor ORedCaras2 in EstructuraRedSurtidor.Values)
                {
                    byte CaraEncuestada2 = ORedCaras2.Cara;

                    //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 09/05/2012
                    if (EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta == 0)
                    {
                        EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta = 1;
                    }

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada2 + "|FactorVolumen: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorVolumen)
                           + " - FactorTotalizador: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorTotalizador)
                           + " - FactorImporte: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorImporte)
                           + " - FactorPrecio: " + Math.Log10(EstructuraRedSurtidor[CaraEncuestada2].FactorPrecio)
                           + " - MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta
                           + " - PredeterminarImporte: " + EstructuraRedSurtidor[CaraEncuestada2].FactorPredeterminacionImporte
                           + " - PredeterminarVolumen: " + EstructuraRedSurtidor[CaraEncuestada2].FactorPredeterminacionVolumen);

                    SWRegistro.Flush();
                }


                //Ciclo Infinito
                while (CondicionCiclo)
                {

                    VerifySizeFile();

                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {
                        if (CondicionCiclo2)
                        {
                            //Si la cara está activa, realizar proceso de encuesta
                            if (ORedCaras.Activa == true)
                            {
                                CaraEncuestada = ORedCaras.Cara;//Cara Asignado 
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias                                                   


                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                EncuentaFinalizada = false;

                                if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                    TomarAccion();

                                EncuentaFinalizada = true;
                            }

                            Thread.Sleep(20);
                        }
                    }
                }
            }

            catch (Exception Excepcion)
            {

                EncuentaFinalizada = true;
                CondicionCiclo2 = true;

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
                //Puerto utilizado por el autorizador para imprimir mensajes de error
                string PuertoAImprimir;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 3;//Antes 5

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                ////Se inicializa el vector de control de fallo de comunicación
                //InconsistenciaDatosRx = false;
                //ErrorComunicacion = false;

                //Arma la trama de Transmision              

                //Reintentos de envio de comando recomendados por Gilbarco
                do
                {
                    ArmarTramaTx(ComandoaEnviar, PrecioNivel1);// DCF OJO   en caso de falla cargar el # de BytesEsperados = 19;

                    if (EsTCPIP)
                        EnviarComando_TCPIP();

                    else
                        EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor


                        if (EsTCPIP)
                            RecibirInformacion_TCPIP();
                        else
                            RecibirInformacion();

                        Reintentos += 1;
                    
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
                string MensajeExcepcion = "Excepcion en el Metodo ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        public void ArmarTramaTx(ComandoSurtidor ComandoTx, bool PrecioNivel1)
        {
            try
            {
                //Asigna a la cara a encustar el comando que fue enviado
                ComandoCaras = ComandoTx;

                TramaTx = new byte[12];
                byte canal = 3;

                TimeOut = 300; 

              
                    switch (ComandoTx)
                    {

                        #region ComandoSurtidor.Estado
                        case (ComandoSurtidor.Estado):           

                                 canal = 3;                              

                                TramaTx[0] = 0x01;      //Byte inicial 
                                TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                                TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                                TramaTx[3] = (byte)(0xB0 | canal);
                                TramaTx[4] = (byte)(0xC0 | 0);// length de los datos a enviar 
                                TramaTx[5] = (byte)(0xA0 | 0);
                                TramaTx[6] = (byte)(0xA0 | 0);//0x00
                                TramaTx[7] = (byte)(0xB0 | 0);
                                TramaTx[8] = (byte)(0xB0 | 0);
                                TramaTx[9] = (byte)(0xB0 | 0);
                                TramaTx[10] = (byte)(0xB0 | 0);
                                TramaTx[11] = 0x04;


                                TimeOut = 500; 
                          break;
                        #endregion;

                        #region ComandoSurtidor.SW_Normal)
                        case (ComandoSurtidor.SW_Normal):

                           canal = 5;
                            
                            TramaTx = new byte[14];

                            TramaTx[0] = 0x01;      //Byte inicial 
                            TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                            TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                            TramaTx[3] = (byte)(0xB0 | canal);
                            TramaTx[4] = (byte)(0xC0 | 1);// length de los datos a enviar 
                            TramaTx[5] = (byte)(0xA0 | 0);
                            TramaTx[6] = (byte)(0xA0 | 0);//0x00
                            TramaTx[7] = (byte)(0xA0 | 0);
                            TramaTx[8] = (byte)(0xA0 | 0);
                            TramaTx[9] = (byte)(0xB0 | 0);
                            TramaTx[10] = (byte)(0xB0 | 0);
                            TramaTx[11] = (byte)(0xB0 | 0);
                            TramaTx[12] = (byte)(0xB0 | 0);
                            TramaTx[13] = 0x04;


                            TimeOut = 200; 
                          break;
                        #endregion;

                        #region ComandoSurtidor.Habilitar_Boquillas)
                        case (ComandoSurtidor.Habilitar_Boquillas):
               
                                 canal = 5;

                                TramaTx = new byte[14];

                                //01 80 81 B5 C1 A0 A3 A3 AF BC BD BC BF 04 

                                TramaTx[0] = 0x01;      //Byte inicial 
                                TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                                TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                                TramaTx[3] = (byte)(0xB0 | canal);
                                TramaTx[4] = (byte)(0xC0 | 1);// length de los datos a enviar (Len = 2+4*N) -1
                                TramaTx[5] = (byte)(0xA0 | 0);
                                TramaTx[6] = (byte)(0xA0 | 3);//0x03  Comando
                                TramaTx[7] = (byte)(0xA0 | 0X03);
                                TramaTx[8] = (byte)(0xA0 | 0x0F);//3F = 0011 1111)   toda las mangueras     
                                TramaTx[9] = (byte)(0xB0 | 0);
                                TramaTx[10] = (byte)(0xB0 | 0);
                                TramaTx[11] = (byte)(0xB0 | 0);
                                TramaTx[12] = (byte)(0xB0 | 0);
                                TramaTx[13] = 0x04;

                                TimeOut = 200; 

                                break;

                        #endregion;
                            
                        #region  ComandoSurtidor.Totales)
                        case (ComandoSurtidor.Totales):
               
                                 canal = 3;

                                TramaTx = new byte[14];
                                int Grado_despacho = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado;

                                //01 80 81 B3 C1 A0 A3 A0 A0 BE B4 B9 B2 04 

                                TramaTx[0] = 0x01;      //Byte inicial 
                                TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                                TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                                TramaTx[3] = (byte)(0xB0 | canal);
                                TramaTx[4] = (byte)(0xC0 | 0x01);// length de los datos a enviar (Len = 2+4*N) -1
                                TramaTx[5] = (byte)(0xA0 | 0);
                                TramaTx[6] = (byte)(0xA0 | 0x03);//0x03  Comando
                                TramaTx[7] = (byte)(0xA0 | 0X00);
                                TramaTx[8] = (byte)(0xA0 | Grado_despacho);
                                TramaTx[9] = (byte)(0xB0 | 0);
                                TramaTx[10] = (byte)(0xB0 | 0);
                                TramaTx[11] = (byte)(0xB0 | 0);
                                TramaTx[12] = (byte)(0xB0 | 0);
                                TramaTx[13] = 0x04;

                                TimeOut = 800; 

                                break;
                        #endregion;

                        #region  ComandoSurtidor.Autorizar)
                        case (ComandoSurtidor.Autorizar):               

                               canal = 5;

                                TramaTx = new byte[14];

                                TramaTx[0] = 0x01;      //Byte inicial 
                                TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                                TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                                TramaTx[3] = (byte)(0xB0 | canal);
                                TramaTx[4] = (byte)(0xC0 | 1);// length de los datos a enviar 
                                TramaTx[5] = (byte)(0xA0 | 0);
                                TramaTx[6] = (byte)(0xA0 | 0);//0x00
                                TramaTx[7] = (byte)(0xA0 | 0);
                                TramaTx[8] = (byte)(0xA0 | 1);//0x01 Autorizar
                                TramaTx[9] = (byte)(0xB0 | 0);
                                TramaTx[10] = (byte)(0xB0 | 0);
                                TramaTx[11] = (byte)(0xB0 | 0);
                                TramaTx[12] = (byte)(0xB0 | 0);
                                TramaTx[13] = 0x04;

                                TimeOut = 400; 
                                break;
                        #endregion;

                        #region  ComandoSurtidor.ParcialDespacho - Fin_Despacho
                        case (ComandoSurtidor.ParcialDespacho):
                        case(ComandoSurtidor.Fin_Despacho):
                        case(ComandoSurtidor.TotalDespacho):
                                
                            canal = 3;

                            TramaTx = new byte[12];


                            TramaTx[0] = 0x01;      //Byte inicial 
                            TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                            TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                            TramaTx[3] = (byte)(0xB0 | canal);
                            TramaTx[4] = (byte)(0xC0 | 0);// length de los datos a enviar 
                            TramaTx[5] = (byte)(0xA0 | 0);
                            TramaTx[6] = (byte)(0xA0 | 2);//0x02
                            TramaTx[7] = (byte)(0xB0 | 0);
                            TramaTx[8] = (byte)(0xB0 | 0);
                            TramaTx[9] = (byte)(0xB0 | 0);
                            TramaTx[10] = (byte)(0xB0 | 0);
                            TramaTx[11] = 0x04;

                            TimeOut = 800; 

                            break;
                        #endregion; 

                        #region  ComandoSurtidor.PrecioDespacho
                        case (ComandoSurtidor.PrecioDespacho):

                            canal = 3;

                                TramaTx = new byte[14];


                                TramaTx[0] = 0x01;      //Byte inicial 
                                TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                                TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                                TramaTx[3] = (byte)(0xB0 | canal);
                                TramaTx[4] = (byte)(0xC0 | 1);// length de los datos a enviar 
                                TramaTx[5] = (byte)(0xA0 | 0);
                                TramaTx[6] = (byte)(0xA0 | 1);//0x00
                                TramaTx[7] = (byte)(0xA0 | 0);
                                TramaTx[8] = (byte)(0xA0 | (EstructuraRedSurtidor[CaraEncuestada].GradoVenta +1));//N
                                TramaTx[9] = (byte)(0xB0 | 0);
                                TramaTx[10] = (byte)(0xB0 | 0);
                                TramaTx[11] = (byte)(0xB0 | 0);
                                TramaTx[12] = (byte)(0xB0 | 0);
                                TramaTx[13] = 0x04;



                                BytesEsperados = 24;

                                TimeOut = 500; 
                            break; 

                        #endregion;
                       
                        #region  ComandoSurtidor.CambiarPrecio
                        case (ComandoSurtidor.CambiarPrecio):

                            
            string strPrecio; // = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioNivel1 ).ToString("X2").PadLeft(8, '0');

         

            strPrecio = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioNivel1 *
                    EstructuraRedSurtidor[CaraEncuestada].FactorPrecio).ToString("X2").PadLeft(8, '0'); //DCF 04_10_16 18:26


            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| strPrecio = " + strPrecio + " - PrecioNivel1: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioNivel1 +
            //   "FactorPrecio: " + EstructuraRedSurtidor[CaraEncuestada].FactorPrecio);
            //                        SWRegistro.Flush();



                              TramaTx = new byte[22];
                                       
                                int[] y = new int[8];

                                for (int i = 0; i < strPrecio.Length; i++)
                                {
                                  //   Dato2 += Convert.ToString(Convert.ToInt16(TramaRx[i] & 0x0F), 16);

                                    y[i] = Convert.ToInt16( Convert.ToString(strPrecio[i]), 16);

                                    TramaTx[9 + i] = (byte)(0xA0 | y[i]); // asignacion del valor del precio en Hex con el valor de 0xA?
                                }
            
                            canal = 5;    

                            TramaTx[0] = 0x01;      //Byte inicial 
                            TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
                            TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
                            TramaTx[3] = (byte)(0xB0 | canal);
                            TramaTx[4] = (byte)(0xC0 | 5);// length de los datos a enviar (Len = 2+4*N) -1
                            TramaTx[5] = (byte)(0xA0 | 0);
                            TramaTx[6] = (byte)(0xA0 | 1);//1x00  Comando
                            TramaTx[7] = (byte)(0xA0 | 0);
                            TramaTx[8] = (byte)(0xA0 | (EstructuraRedSurtidor[CaraEncuestada].GradoCara + 1));//N (Nuero de Manguera)
                            TramaTx[17] = (byte)(0xB0 | 0);
                            TramaTx[18] = (byte)(0xB0 | 0);
                            TramaTx[19] = (byte)(0xB0 | 0);
                            TramaTx[20] = (byte)(0xB0 | 0);
                            TramaTx[21] = 0x04;

                            TimeOut = 500; 

                            break;
                        #endregion;

                        #region  ComandoSurtidor.Predeterminar
                        case (ComandoSurtidor.Predeterminar):

                             int T = 0;//Deletes la predeterminacion

                             int pre = 0;
                             

                            //* 100 para Volumen 
                            //*10 para Importe 
                             if (EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte == 0) // 14-10-2016 DCF Prede
                             {
                                 EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte = 10; 
                             }
                            if (EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen == 0)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen = 100; 
                            }


                             if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                             {
                                 T = 2;//2 = Presets the V value of the computing head price.
                                 EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false; // Prede 12-10-2016

                                 //pre = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado) * 10; //Mexico 

                                 pre = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado) * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionImporte;// 14-10-2016 DCF Prede

                                 SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|ValorPredeterminado = " + pre);//Borra 
                                 SWRegistro.Flush();

                             }
                            else
                             {
                                 T = 1; //1 = Presets the V value of the computing head volume.
                                 EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen = false; // Prede 12-10-2016

                                 //pre = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado) * 100; //Mexico OK para Volumen 

                                 pre = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado) * EstructuraRedSurtidor[CaraEncuestada].FactorPredeterminacionVolumen;// 14-10-2016 DCF Prede

                                 SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|ValorPredeterminado = " + pre); //Borrar 
                                 SWRegistro.Flush();

                             }

                     canal = 5;



            TramaTx = new byte[22];






            // pre = Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado) * 100; OK para VOlumen;  en Precio se pasaba * 10 se programa 35 ---> 350 se detenia 

            string Preset = pre.ToString("X2").PadLeft(8, '0');

            int[] x = new int[8];

            for (int i = 0; i < 8; i++)
            {
                //   Dato2 += Convert.ToString(Convert.ToInt16(TramaRx[i] & 0x0F), 16);

                x[i] = Convert.ToInt16(Convert.ToString(Preset[i]), 16);

                TramaTx[9 + i] = (byte)(0xA0 | x[i]); // asignacion del valor del precio en Hex con el valor de 0xA?

            }


            TramaTx[0] = 0x01;      //Byte inicial 
            TramaTx[1] = 0x80;     // Sender Address siempre sera 80 PC 
            TramaTx[2] = (byte)(0x80 | CaraEncuestada);  //Addres Cara o Lado
            TramaTx[3] = (byte)(0xB0 | canal);
            TramaTx[4] = (byte)(0xC0 | 0x05);// length de los datos a enviar (Len = 2+4*N) -1

            TramaTx[5] = (byte)(0xA0 | 0);
            TramaTx[6] = (byte)(0xA0 | 0x02);//0x02  Comando

            TramaTx[7] = (byte)(0xA0 | 0X00); //T
            TramaTx[8] = (byte)(0xA0 | T);//T
                            
            TramaTx[17] = (byte)(0xB0 | 0);
            TramaTx[18] = (byte)(0xB0 | 0);
            TramaTx[19] = (byte)(0xB0 | 0);
            TramaTx[20] = (byte)(0xB0 | 0);
            TramaTx[21] = 0x04;



            TimeOut = 500; 

                            break;
                        #endregion;

                    }




                    


                    CRC_Calculo();
                
         

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Comando " + ComandoTx + ":" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        private bool ComprobarIntegridadTrama()
        {
            try
            {

                int CRC = 0;
                int Carry;

                int conta = TramaRx.Length - 5;

                for (int j = 1; j < conta; j++)
                {
                    CRC = CRC ^ ((TramaRx[j]) << 8);
                    for (int i = 0; i < 8; i++)
                    {
                        Carry = CRC & 0x8000;
                        if (Carry != 0)
                        {
                            CRC = (CRC << 1) ^ 0x1021;
                        }
                        else
                        {
                            CRC <<= 1;
                        }
                    }

                }

                string sCRC = CRC.ToString("X2").PadLeft(4, '0'); //convierte el CRC de tipo INT a tipo String
                byte[] ArrayCRC = new byte[4];
                int len = TramaRx.Length;

                ArrayCRC[0] = Convert.ToByte(sCRC.Substring(sCRC.Length - 1, 1), 16); //convierte el CRC a tipo Byte
                ArrayCRC[1] = Convert.ToByte(sCRC.Substring(sCRC.Length - 2, 1), 16);
                ArrayCRC[2] = Convert.ToByte(sCRC.Substring(sCRC.Length - 3, 1), 16); //convierte el CRC a tipo Byte
                ArrayCRC[3] = Convert.ToByte(sCRC.Substring(sCRC.Length - 4, 1), 16);


                if ((TramaRx[len - 2] == (0xB0 | ArrayCRC[0])) && (TramaRx[len - 3] == (0xB0 | ArrayCRC[1])) &&
                    (TramaRx[len - 4] == (0xB0 | ArrayCRC[2])) && (TramaRx[len - 5] == (0xB0 | ArrayCRC[3])))
                {
                    CRC_RX_ = true;
                   

                }
                else
                {
                    CRC_RX_ = false;


                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|Comando " + ComandoCaras + ":" + "Falla en el CRC Recibido / Calculado ");
                    SWRegistro.Flush();
                    
                }

                return CRC_RX_;

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Calculo del CRC_RX: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Comando " + ComandoCaras + ":" + MensajeExcepcion);
                SWRegistro.Flush();

                CRC_RX_ = false;

                return CRC_RX_;
            }
        }


        private void CRC_Calculo()
        {

            try
            {

                int CRC = 0;
                int Carry;

                int conta = TramaTx.Length - 5;

                for (int j = 1; j < conta; j++)
                {
                    CRC = CRC ^ ((TramaTx[j]) << 8);
                    for (int i = 0; i < 8; i++)
                    {
                        Carry = CRC & 0x8000;
                        if (Carry != 0)
                        {
                            CRC = (CRC << 1) ^ 0x1021;
                        }
                        else
                        {
                            CRC <<= 1;
                        }
                    }

                }

                string sCRC = CRC.ToString("X2").PadLeft(4, '0'); //convierte el CRC de tipo INT a tipo String
                byte[] ArrayCRC = new byte[4];
                int len = TramaTx.Length;

                ArrayCRC[0] = Convert.ToByte(sCRC.Substring(sCRC.Length - 1, 1), 16); //convierte el CRC a tipo Byte
                ArrayCRC[1] = Convert.ToByte(sCRC.Substring(sCRC.Length - 2, 1), 16);
                ArrayCRC[2] = Convert.ToByte(sCRC.Substring(sCRC.Length - 3, 1), 16); //convierte el CRC a tipo Byte
                ArrayCRC[3] = Convert.ToByte(sCRC.Substring(sCRC.Length - 4, 1), 16);

                TramaTx[len - 2] = (byte)(0xB0 | ArrayCRC[0]);
                TramaTx[len - 3] = (byte)(0xB0 | ArrayCRC[1]);
                TramaTx[len - 4] = (byte)(0xB0 | ArrayCRC[2]);
                TramaTx[len - 5] = (byte)(0xB0 | ArrayCRC[3]);

                TramaTx[len - 1] = 0x04;//Byte final 

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Calculo del CRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Comando " + ComandoCaras + ":" + MensajeExcepcion);
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

                    //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                    eco = Convert.ToByte(TramaTx.Length);

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


                  
                    Thread.Sleep(200);//para efecto de pruebas con surtidor virtual

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

                try
                {
                    //   SWTramas.WriteLine(
                    //DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    //DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    //DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    //DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    //"|" + CaraID + "|*6|Antes de verificar conexion");

                    //SWTramas.Flush();


                    //   SWTramas.WriteLine(
                    //DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    //DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    //DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    //DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    //"|" + CaraID + "|*8|Despues de Verificar Conexion");

                    //SWTramas.Flush();
                    //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                    eco = Convert.ToByte(TramaTx.Length); // 11_10_2016 DCF

                    Stream.Write(TramaTx, 0, TramaTx.Length);
                    Stream.Flush();
                }
                catch (System.Net.Sockets.SocketException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   Sockets.SocketException ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {
                        VerificarConexion();
                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();

                    }
                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:   VerificarConexion ");
                        SWTramas.Flush();

                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();

                    }

                }
                catch (System.Exception)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        //Stream.Write(TramaTx, 0, TramaTx.Length);
                        //Stream.Flush();


                    }
                    catch (Exception)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();

                    }

                }


                /////////////////////////////////////////////////////////////////////////////////
                //LOGUEO DE TRAMA TRANSMITIDA
                string strTrama = "";
                for (int i = 0; i <= TramaTx.Length - 1; i++)
                    strTrama += TramaTx[i].ToString("X2") + "|";


                SWTramas.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    "|" + CaraID + "|Tx|" + strTrama + " ~ " + ComandoCaras); // ComandoCaras //DCF 24/05/2017 

                SWTramas.Flush();


                //Thread.Sleep(TimeOut);//Tiempo 
                Thread.Sleep(TimeOut);//Tiempo solo para pruebas de loop 

            }
            catch (System.IO.IOException ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Reintento de envio de comando");
                SWTramas.Flush();
                SWRegistro.WriteLine(DateTime.Now + "|Reintento de envio de comando| " + ex.Message);
                SWTramas.Flush();
                //Thread.Sleep(150);
                //VerificarConexion();
                //Stream.Write(TramaTx, 0, TramaTx.Length);
                //Stream.Flush();
            }
        }

        public static void CallBackMethod(IAsyncResult asyncresult)
        {

        }



        public void VerificarConexion()
        {
            int iReintento = 0;
            string Comando = "";
            try
            {
                if (ClienteGilbarco == null)
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
                            ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteGilbarco == null)
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

                        if (ClienteGilbarco != null)
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
                if (!this.ClienteGilbarco.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    Error_ConexionTCP = true; //DCF 30/08/2017

                    try
                    {
                        ClienteGilbarco.Client.BeginDisconnect(true, callBack, ClienteGilbarco);

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



                while (!this.ClienteGilbarco.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteGilbarco.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteGilbarco);
                        //ClienteGilbarco.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteGilbarco.Client.Connected)
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
                this.Stream = ClienteGilbarco.GetStream();
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
                ClienteGilbarco.Close();
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
                ClienteGilbarco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteGilbarco.GetStream();
                if (this.ClienteGilbarco.Client.Connected == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Conexion Abierta");
                    SWRegistro.Flush();


                    Error_ConexionTCP = false; //DCF 30/08/2017

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
                if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = BytesEsperados + eco;
                BytesEsperados_Extended = BytesEsperados_Extended + eco;
                // byte[] TramaRxTemporal = new byte[BytesEsperados_Extended];

                if (Stream == null)
                {
                    this.ErrorComunicacion = true;
                    return;
                }

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);



                if (Stream.DataAvailable)
                {
                    //if (EstructuraRedSurtidor[CaraEncuestada].Gilbarco_Extended)
                    //    TramaRxTemporal = new byte[BytesEsperados_Extended];
                    //else
                    //    TramaRxTemporal = new byte[BytesEsperados];


                    //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor
                    byte[] TramaRxTemporal = new byte[1024]; // para que tome toda lo escrito en el buffer problemas con los byte esperado cuando se tiene mas grado configurado dentro del surtidor 



                    // Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }



                       //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor


                        //LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                        //ErrorComunicacion = false;


                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        ////Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        // PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                        //PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length - eco];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                            TramaRx[i] = TramaRxTemporal[i + eco];


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
                                "|" + CaraID + "|Rx|" + strTrama + "| #| " + TramaRx.Length);

                            SWTramas.Flush();

                        }


                    //Permite loguear todo  lo que llegue como RX 25/06/2016
                        //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados



                        if (ComandoCaras == ComandoSurtidor.PrecioDespacho && BytesEsperados != Bytes_leidos) //  DCF 17/07/2017
                       {

                           SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados + "- ComandoCaras:  " + ComandoCaras);
                           SWRegistro.Flush();


                           BytesEsperados = 0;
                           Bytes_leidos = 0; // provoca el error para reenviar la consulta del precio. 

                           this.ErrorComunicacion = true;
                       }

                        if (Bytes_leidos > 0 && TramaRx.Length > 0 && TramaRx[0] == 0x01 && ComprobarIntegridadTrama()) // TramaRx[0] == 0x01 DCF 17/07/2017
                        {                    
                                /////////////////////////////////////////////////////////////////////////////////

                                AnalizarTrama();

                                //Se inicializa el vector de control de fallo de comunicación
                                InconsistenciaDatosRx = false;
                                ErrorComunicacion = false;

                        }

                    //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor 
                    else if (ErrorComunicacion == false)
                    {

                        //SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        //SWRegistro.Flush();

                        this.ErrorComunicacion = true;
                    }

                    //SWRegistro.Flush();
                }
                else if (ErrorComunicacion == false)
                {

                   this.ErrorComunicacion = true;

                }


            }
            catch (Exception Excepcion)
            {
                LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public void LimpiarSockets()
        {
            try
            {
                //ClienteGilbarco.Client.Disconnect(false);  
                ClienteGilbarco.Client.Close();
                ClienteGilbarco.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteGilbarco = null;
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
                    //if (Bytes_leidos == BytesEsperados || Bytes_leidos == BytesEsperados_Extended)
                    //{
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
                            "|" + CaraID + "|Rx|" + strTrama );

                        SWTramas.Flush();

                    }

                  

                    if (Bytes_leidos > 0  && TramaRx.Length > 0 && ComprobarIntegridadTrama())
                    {
                    
                        AnalizarTrama();

                        //11_10_2016 DCF
                        //Se inicializa el vector de control de fallo de comunicación
                        InconsistenciaDatosRx = false;
                        ErrorComunicacion = false;
                        
                    }
                    else if (ErrorComunicacion == false)
                    {

                        this.ErrorComunicacion = true;
                        
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error Bytes_leidos = " + Bytes_leidos + " - Bytes_Enviado = " + TramaTx.Length);

                        if (Bytes_leidos == TramaTx.Length)
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error| solo se recibe el ECO" );


                        SWRegistro.Flush();



                    }

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
                    case (ComandoSurtidor.Estado):
                        AsignarEstado();
                        break;
                    case (ComandoSurtidor.TotalDespacho): // 4
                        RecuperarDatosFindeVenta();
                        break;
                    case (ComandoSurtidor.Totales): //5
                        RecuperarTotalizadores();
                        break;
                    case (ComandoSurtidor.ParcialDespacho):// 6
                     RecuperarParcialesdeVenta();
                       break;
                    case(ComandoSurtidor.PrecioDespacho):
                       RecuperarPrecioVenta();
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
                //Se separan el Codigo del estado 
                byte CodigoEstado = Convert.ToByte(TramaRx[8]);

                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;

                byte CaraqueResponde = Convert.ToByte(TramaRx[1] & (0x0F));
                //Evalua si la informacion que se recibio como respuesta corresponde a la cara que fue encuestada
                if (CaraqueResponde == CaraEncuestada && TramaRx[4] == 0xC4 && TramaRx[5] == 0xA8 && TramaRx[6] == 0xA0)
                {
                    InconsistenciaDatosRx = false; //No hubo error por fallas en datos
                    
                    //Asigna Estado
                    switch (CodigoEstado)
                    {
                        case (0xA0):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Power_on;
                            break;

                        case (0xA1):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Modo_Normal;

                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Finaliza venta en Estado Espera");
                                SWRegistro.Flush();
                                //EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                            }
                            else
                                if (TramaRx[9] == 0xA0 && TramaRx[10] == 0xA0)
                                {

                                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;
                                }

                            break;

                        case (0xA2):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespacho;
                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado |0xA2 ** " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                            //SWRegistro.Flush();
                            break;

                        case (0xA3):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Autorizada;
                            break;

                        case (0xA4):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                            break;

                        case (0xA5):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoA;
                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado |0xA5 *** " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                            //SWRegistro.Flush();
                            break;

                        case (0xA6):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Distribution_stop;
                            break;
                            
                        case (0xA7):
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Authorization_denied;
                            break;


                        default:
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Indeterminado;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|EstadoIndeterminado: " + CodigoEstado);
                            SWRegistro.Flush();
                            InconsistenciaDatosRx = true;
                            break;
                    }



                    byte Boquilla_ON0 = Convert.ToByte(TramaRx[10] & (0x0F));

                    switch (Boquilla_ON0)
                    {
                        case (0x01)://1
                        case (0x02)://2
                        case (0x04)://3
                        case (0x08)://4
                            {

                                if (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho &&
                                    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespachoA &&
                                    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespacho &&
                                    EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizada) //11_10_2016 DCF
                                {
                                    EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = Convert.ToByte(TramaRx[10] & (0x0F) - 1);
                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado |%% Boquilla_ON0" + Boquilla_ON0+ " - " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                    //SWRegistro.Flush();

                                }

                                break;
                            } 

                    }

                    byte Boquilla_ON1 = Convert.ToByte(TramaRx[9] & (0x0F));

                    switch (Boquilla_ON1)
                    {
                        case (0x01)://5
                        case (0x02)://6

                            if ( EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho &&
                                 EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespachoA &&
                                 EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.FinDespacho &&
                                 EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizada ) //11_10_2016 DCF
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;
                                EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = Convert.ToByte(TramaRx[10] & (0x0F) - 1);

                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado |%% Boquilla_ON1" + Boquilla_ON0 + " - " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                                //SWRegistro.Flush();
                            }
                           break;

                    }

                  

                    //Almacena en archivo el estado actual del surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado +
                            " - " + CodigoEstado.ToString("X2").PadLeft(2, '0'));
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Comando: " + ComandoCaras + " - Cara que Responde: " + CaraqueResponde);
                    SWRegistro.Flush();
                    InconsistenciaDatosRx = true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AsignarEstado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void Restablecer_MODO_Normal()
        {
            if (ProcesoEnvioComando(ComandoSurtidor.SW_Normal, false))
            {

                if (ProcesoEnvioComando(ComandoSurtidor.Habilitar_Boquillas, false))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Restablecer_MODO_Normal");
                    SWRegistro.Flush();
                }
            }
            else
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error| Fallo Restablecer_MODO_Normal");
            SWRegistro.Flush();
        }

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        public void TomarAccion()
        {
            try
            {
                int Reintentos = 0;
                //Puerto utilizado por el autorizador para imprimir mensajes de error
                String PuertoAImprimir;

                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {


                    #region Power_On
                    case (EstadoCara.Power_on):

                        Restablecer_MODO_Normal();

                        break;




                    #endregion


                    /***************************ESTADO EN ESPERA***************************/
                    #region Estado en Espera
                    case (EstadoCara.Espera):

                        //cambio de precio para un cliente X                       
                        if (EstructuraRedSurtidor[CaraEncuestada].AplicaCambioPrecioCliente)
                        {
                            try
                            {

                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].CambioPrecioVentaActivo)
                                {
                                    // Cambio de precio / MultiplicadorPrecioVenta para el cambio de producto 24-03-2012
                                    //control del precio en el cambio de producto
                                    //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 =
                                    //   EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 /
                                    //    EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;

                                    decimal Precio_BD_PrecioNivel1 =
                                          (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1); //DCF 29-10-2014 corrección de cambio de precio


                                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count > 0)
                                        if ((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 != 0) && (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1 !=
                                         EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1))
                                        {
                                            //retornar a precio Inicial cargado en la apertura de turno
                                            //CambiarPrecioVenta(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1);
                                            //EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].CambioPrecioVentaActivo = false;

                                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Cambio de Precio en Estado Espera. PrecioNivel1:" + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1);
                                            //SWRegistro.Flush();

                                            CambiarPrecioVenta(Precio_BD_PrecioNivel1);
                                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].CambioPrecioVentaActivo = false;

                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Cambio de Precio en Estado Espera. PrecioNivel1:" + Precio_BD_PrecioNivel1);
                                            SWRegistro.Flush();


                                        }
                                }
                            }
                            catch (Exception ex)
                            {
                                int xx = 0;
                                xx = 1;
                            }
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
                                    EstructuraRedSurtidor[CaraEncuestada].Guid = "";
                                    CaraEnReposo(CaraID, mangueraColgada);
                                }
                            }
                            //else
                            //{
                            //    Eventos.InformarCaraEnReposo( CaraID,  mangueraColgada);
                            //}

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cara en Espera. Manguera: " + mangueraColgada);
                            SWRegistro.Flush();

                            EstructuraRedSurtidor[CaraEncuestada].Manguera_ON = true; //Para controlar el envio en el Evento|Informar Lectura Inicial de Venta  --- DCF 05-10-2016 


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
                    case (EstadoCara.Despacho):
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
                        if(ProcesoEnvioComando(ComandoSurtidor.ParcialDespacho, false))
                        {

                    

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
                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara)
                        {
                            EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara = false;
                            ProcesoEnvioComando(ComandoSurtidor.Stop, false);
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
                    // case (EstadoCara.FinDespachoA)://12-10-2016
                    case (EstadoCara.FinDespacho): //12-10-2016
                    case (EstadoCara.FinDespachoForzado)://12-10-2016 -- //20/01/2017 DCF
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
                        { 
                            //SW.WriteLine(DateTime.Now + "  Cara " + CaraEncuestada + ": proceso de fin de venta lanzado ESVENTAPARCIAL: " + Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial));
                            //{
                            //Thread HiloVenta = new Thread(ProcesoFindeVenta);
                            //HiloVenta.Start();
                            ProcesoFindeVenta();
                        }
                        else
                        {
                            //envio de comando de SW_Normalizado
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Venta finalizada sin Venta Parcial ");
                            SWRegistro.Flush();
                            Restablecer_MODO_Normal();
 
                        }
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

                                Reintentos = 0;
                                do
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

                                            string guidautorizacion = Guid.NewGuid().ToString();
                                            EstructuraRedSurtidor[CaraEncuestada].Guid = guidautorizacion;

                                            // Eventos.RequerirAutorizacion( CaraID,  IdProducto,  IdManguera,  Lectura);
                                            string[] DatosAutorizacion = { CaraID.ToString(), IdProducto.ToString(), IdManguera.ToString(), Lectura, guidautorizacion };

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
                                    
                                    Reintentos++;
                                } while (Reintentos <= 3);
                            }
                        

                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
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

                           
                                string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");

                                if (EstructuraRedSurtidor[CaraEncuestada].Manguera_ON)
                                {
                                    EstructuraRedSurtidor[CaraEncuestada].Manguera_ON = false; //Para controlar el envio en el Evento|Informar Lectura Inicial de Venta  --- DCF 05-10-2016 


                                    if (LecturaInicialVenta != null)
                                    {

                                        LecturaInicialVenta(CaraID, strLecturasVolumen);

                                        //Loguea Evento de envio de lectura
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Venta: " +
                                            strLecturasVolumen);
                                        SWRegistro.Flush();
                                    }
                                }
                                //else
                                //{
                                //    Eventos.InformarLecturaInicialVenta( CaraID,  strLecturasVolumen);
                                //}

                        

                                //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                //    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1;


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

                                        ProcesoEnvioComando(ComandoSurtidor.SW_Normal, false);
                                        Thread.Sleep(30);

                                        ProcesoEnvioComando(ComandoSurtidor.Autorizar, false);
                                        Reintentos++;
                                        Thread.Sleep(30);

                                        

                                        ProcesoEnvioComando(ComandoSurtidor.Estado, false);

                                    } while (EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Autorizada &&
                                        EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho && (Reintentos <= 3));

                                    //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizada ||
                                        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho) //12-10-2016
                                    {
                                        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                                    }
                                }
                            
                        }
                        
                        break;
                    #endregion


                    case( EstadoCara.Authorization_denied):

                        Restablecer_MODO_Normal();

                         break;

                    case (EstadoCara.Autorizada):
                    case (EstadoCara.FinDespachoA)://12-10-2016

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
                string GuidVenta = data[4];

                if (AplicaServicioWindows)
                {
                    if (AutorizacionRequerida != null)
                    {
                        AutorizacionRequerida(CaraTmp, IdProducto, IdManguera, Lectura,GuidVenta);
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





        //REALIZA PROCESO DE FIN DE VENTA
        public void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;

                //int Reintentos = 0;
                decimal VolumenCalculado = new decimal();

                //Obtiene los Valores Finales de la Venta (Pesos y Metros cubicos despachados)
                if (ProcesoEnvioComando(ComandoSurtidor.TotalDespacho, false))
                {
                    //Si el grado que responde está dentro del la lista de grados
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >= EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura Final de Venta en el Grado: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                        SWRegistro.Flush();// escribir el grado que realizo la venta DCF 29-10-2013

                        //Obtiene la Lectura Final de la Venta
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoVenta;
                        TomarLecturas();

                        //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                        if (EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado != EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                        {
                            /*- WBC: Modificado el 10/07/2009 ---*/
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado +
                                " - Grado que vendio: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                            SWRegistro.Flush();

                        }

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
                                    if (EstructuraRedSurtidor[CaraEncuestada].Volumen < VolumenCalculado - Convert.ToDecimal(0.05) ||
                                        EstructuraRedSurtidor[CaraEncuestada].Volumen > VolumenCalculado + Convert.ToDecimal(0.05))
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Volumen Calculado: " + VolumenCalculado +
                                            " - Volumen Reportado: " + EstructuraRedSurtidor[CaraEncuestada].Volumen);
                                        SWRegistro.Flush();
                                        EstructuraRedSurtidor[CaraEncuestada].Volumen = VolumenCalculado;
                                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                                            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;//DCF el precio de venta es 10000 pero se le envia al surtidor 1000, 
                                    }
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

                        //Solicitud de Precio.
                        if (ProcesoEnvioComando(ComandoSurtidor.PrecioDespacho, false))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio de Despacho = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                            SWRegistro.Flush();
                        }


                     

                        //Si se realizó una venta con valores de m3 y $ mayor que cero
                        if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                        {
                         
                            //Aqui va el lanzamiento del evento de fin de venta
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                            ////Calcula la correspondencia entre el Volumen, el Precio y el Importe
                            //decimal TotalVentaCalculada = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                            //    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|MultiplicadorPrecioVenta = " + EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta); //Borra
                            SWRegistro.Flush(); //Borrar solo para Prueba


                            if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta > 0)// Cntrol DCF 03/05/2017 
                            {
                                ////Calcula la correspondencia entre el Volumen, el Precio y el Importe //DCF
                                decimal TotalVentaCalculada = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                                    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta;//DCF el precio de venta es 10000 pero se le envia al surtidor 1000, 

                                //decimal PorcentajeVenta = (TotalVentaCalculada * 2) / 100;   //Porcentaje del 5% //DCF 02-08-2011 corrección para Perú 
                                decimal PorcentajeVenta = ((TotalVentaCalculada * 5) / 100);   //terpel 
                                if (EstructuraRedSurtidor[CaraEncuestada].TotalVenta == 0 ||
                                    //EstructuraRedSurtidor[CaraEncuestada].TotalVenta > TotalVentaCalculada + 100/ EstructuraRedSurtidor[CaraEncuestada].FactorImporte ||
                                    //EstructuraRedSurtidor[CaraEncuestada].TotalVenta < TotalVentaCalculada - 100 / EstructuraRedSurtidor[CaraEncuestada].FactorImporte) //para colombia
                                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta > (TotalVentaCalculada + (PorcentajeVenta)) ||
                                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta < (TotalVentaCalculada - (PorcentajeVenta))) // Para Peru 
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID +
                                        "|Inconsistencia|Valor recibido en : " + EstructuraRedSurtidor[CaraEncuestada].TotalVenta +
                                        " - Calculado: " + TotalVentaCalculada +
                                        " - PorcentajeVenta = " + PorcentajeVenta + "TotalVenta > " + (TotalVentaCalculada + (PorcentajeVenta)) + "TotalVenta < " + (TotalVentaCalculada - (PorcentajeVenta)));
                                    SWRegistro.Flush();
                                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = TotalVentaCalculada;

                                }


                            }


                            //Dispara evento al programa principal si la venta es diferente de 0
                            string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                            string strPrecio = (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta * EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta).ToString("N3");
                            string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                            string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                            string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                            string bytProducto = Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].IdProducto);
                            int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].MangueraBD;

                            //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada


                            //Loguea evento Fin de Venta
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Finalizacion Venta. Importe: " + strTotalVenta +
                                " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                                " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                            SWRegistro.Flush();

                            string PresionLLenado = "0";
                            string[] Args = { CaraID.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

                            //                      string Args = CaraEncuestada.ToString() + "|" + strTotalVenta.ToString() + "|" + strPrecio.ToString() + "|" + strLecturaFinalVenta.ToString() + "|" + strVolumen.ToString() + "|" + bytProducto.ToString() + "|" + IdManguera.ToString() + "|" + PresionLLenado.ToString() + "|" + strLecturaInicialVenta.ToString();

                            //Thread HiloFinalizacionVenta = new Thread(InformarFinalizacionVenta);
                            //HiloFinalizacionVenta.Start(Args);
                            ThreadPool.QueueUserWorkItem(new WaitCallback(InformarFinalizacionVenta), Args);



                            ProcesoEnvioComando(ComandoSurtidor.SW_Normal, false);
                           
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
                    byte CaraqueResponde = Convert.ToByte((TramaRx[1] & (0x0F)));
                    if (CaraqueResponde == CaraEncuestada)
                    {

                            //////Se obtiene el Precio con que se realizo la venta
                            //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                            //    ObtenerValor(12, 15) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;


                        //Se obtiene el Dinero despachado
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta =
                            ObtenerValor(19, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;

                            //Se obtiene el Volumen despachado
                            EstructuraRedSurtidor[CaraEncuestada].Volumen =
                                ObtenerValor(27, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                      

                            //Se Optiene el grado por donde se despacho
                            EstructuraRedSurtidor[CaraEncuestada].GradoVenta = Convert.ToByte(TramaRx[10] & 0x0F);
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
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarDatosFindeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();

                this.ErrorComunicacion = true; //DCF 24/05/2017              

                Thread.Sleep(1000);
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

                //Cambia el precio si es apertura de turno
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                    SWRegistro.Flush();
                    CambiarPrecios(EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count);
                }

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
                        }
                    }
                    //else
                    //{

                    //    Eventos.InformarLecturaFinalTurno( LecturasEnvio);
                    //}
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Finales de turno");
                    SWRegistro.Flush();
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                }
                //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    if (AplicaServicioWindows)
                    {
                        if (LecturaTurnoAbierto != null)
                        {
                            LecturaTurnoAbierto(LecturasEnvio);
                        }
                    }
                    //else
                    //{

                    //    Eventos.InformarLecturaInicialTurno( LecturasEnvio);
                    //}
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Iniciales de turno");
                    SWRegistro.Flush();
                    EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
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

                //Realiza hasta tres reintentos de toma de lecturas si hubo error en la obtención
                do
                {
                    Reintentos += 1;
                    if (!ProcesoEnvioComando(ComandoSurtidor.Totales, false))
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
        public void RecuperarTotalizadores()
        {
            try
            {
                //Variable que almacena el Grado que esta recibiendo del surtidor
                byte GradoSurtidor = new int();


                //Obtiene todos los valores de cada uno de los Grados que tiene el surtidor
                    int i = 0;


                    //Obtiene todos los valores de Totalizadores y Precios de cada uno de los Grados que tiene el surtidor
                    
                       
                        try
                        {
                            //Obtiene el grado de la trama recibida
                            GradoSurtidor = Convert.ToByte((TramaRx[8] & 0x0F));
                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >= GradoSurtidor)
                            {
                                //Obtiene las lecturas de volumen en el grado respectivo                        
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[GradoSurtidor].Lectura =
                                    ObtenerValor(19,8) / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;                               

                            }
                        }
                        catch (Exception ex)
                        {
                            string MensajeExcepcion = "Excepcion en el Metodo RecuperarTotalizadores 1: " + ex;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                            SWRegistro.Flush();
                        }
                        //Ciclo realizado hasta que encuentre el final de la trama 0xFB
                  



                    InconsistenciaDatosRx = false;
                
                
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
                 //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1;

                 //   SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| Precio Tomado del Configurador - PrecioNivel1 = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                 //   SWRegistro.Flush();
             

               

                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = ObtenerValor(19, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;

                    EstructuraRedSurtidor[CaraEncuestada].Volumen = ObtenerValor(27, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;
               
                
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarParcialesdeVenta: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }



        public void RecuperarPrecioVenta()
        {

            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = ObtenerValor(11, 8) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;


            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio  PrecioVenta  = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta + " -FactorPrecio: " + EstructuraRedSurtidor[CaraEncuestada].FactorPrecio
            //    + " -EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 = " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioSurtidorNivel1);

             EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

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
                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta = i; //utilizada para vaidar el cambio de precio ??
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
                            ArmarTramaTx(ComandoSurtidor.CambiarPrecio, PrecioNivel1);
                           
                            if (EsTCPIP)
                            {
                                EnviarComando_TCPIP();
                                RecibirInformacion_TCPIP();
                            }
                            else
                                EnviarComando();


                            //consultar precio Cambiado??'
                            Thread.Sleep(20);

                            ArmarTramaTx(ComandoSurtidor.PrecioDespacho, false);

                            if (EsTCPIP)
                            {
                                EnviarComando_TCPIP();
                                RecibirInformacion_TCPIP();
                            }
                            else
                            {
                                EnviarComando();
                                RecibirInformacion();

                            }


                           
                            Reintentos += 1;

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 ==
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1)
                            {
                                NumeroDePreciosACambiar -= 1;
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio = true;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio de Precio Exitoso = " +
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1);
                                
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

                      
                       if( ProcesoEnvioComando(ComandoSurtidor.CambiarPrecio, false));

                       Thread.Sleep(20);
                       

                        ProcesoEnvioComando(ComandoSurtidor.PrecioDespacho, false);
                   
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
                

                    //Si se va a predeterminar por importe y el valor a predeterminar NO supera las 6 cifras
                    if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte &&
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * EstructuraRedSurtidor[CaraEncuestada].FactorImporte <= 999999)
                    {

                        ArmarTramaTx(ComandoSurtidor.Predeterminar, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Valor de Importe Predeterminado: " +
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
                        ArmarTramaTx(ComandoSurtidor.Predeterminar, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Valor de Volumen Predeterminado (importe convertido): " +
                           EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();

                        EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false; //DCF

                    }
                    
                if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen) // Prede 12-10-2016
                    {
                        ArmarTramaTx(ComandoSurtidor.Predeterminar, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Valor de Volumen Predeterminado: " +
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
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo Predeterminar: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

   
        public void VerifySizeFile()
        {
            try
            {

                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                //FileInfo FileInf = new FileInfo(ArchivoTramas);
                 FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddhhmmss") + "-H2P_Safe-Sucesos(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(Archivo);
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

        public decimal ObtenerValor(int PosicionInicial, int Longitud)
        {
            try
            {
                decimal Valor = new decimal();
                string Dato = "";

                for (int i = PosicionInicial; i <= (PosicionInicial + Longitud - 1); i++)
                {
                    Dato += Convert.ToString(Convert.ToInt16(TramaRx[i] & 0x0F), 16);
                }

                Valor =Convert.ToDecimal( uint.Parse(Dato, System.Globalization.NumberStyles.AllowHexSpecifier));

                return Valor;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ObtenerValor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion + " -Comando:" + ComandoCaras);
                SWRegistro.Flush();

                this.ErrorComunicacion = true; //DCF 24/05/2017

                Thread.Sleep(1000);//Tiempo solo para pruebas de loop 
                
                return 1;
            }
        }
        #endregion

        #region EVENTOS DE LA CLASE

        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
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
       public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
      //public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid)
          {
            try
            {


                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                        " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada + " - Placa: " + Placa); //13-10-2016 DCF
                SWRegistro.Flush();

                byte CaraTmp;

                CaraTmp = ConvertirCaraBD(Cara); 
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {


                   // if (guid == EstructuraRedSurtidor[CaraTmp].Guid)//Generar GUID autorizacion 02/02/2016
                    {
                        //Loguea evento                
                        //13-10-2016 DCF CaraTmp logueo de la cara 
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                                " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                                " - Gerenciada: " + EsVentaGerenciada + " Precio: " + Precio + " Guid recibido: " + guid + " Guid protocolo: " + EstructuraRedSurtidor[CaraTmp].Guid);
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
                            {
                                EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = true; //DCF 14-10-2016
                                EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;                          

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|PredeterminarVolumen  " + EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen); //13-10-2016 DCF
                                SWRegistro.Flush();
                            }

                            if (TipoProgramacion == 0)                            
                            {
                                EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                                EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false; //DCF 14-10-2016


                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|PredeterminarImporte  " + EstructuraRedSurtidor[CaraTmp].PredeterminarImporte); //13-10-2016 DCF
                                SWRegistro.Flush();

                            }
                        }

                    }
                    //else
                    //{
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Venta NO Autorizada. ELGUID DE LA SOLICITUD DE AUTORIZACION NO CORRESPONDE AL DE LA APROPACION DE LA AUTORIZACION: Valor Programado " + ValorProgramado +
                    //                                " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                    //                                " - Gerenciada: " + EsVentaGerenciada + " Precio: " + Precio + " Guid Recibido: " + guid + " Guid generado en protocolo: " + EstructuraRedSurtidor[CaraTmp].Guid);
                    //    SWRegistro.Flush();
                    
                    //}


                   

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


        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();


                //SI no se tiene conexion con el server cancelar la apertura:


                if (Error_ConexionTCP) //DCF 30/08/2017)
                {
                    if (AplicaServicioWindows)
                    {
                        bool EstadoTurno = false;
                        string MensajeErrorLectura = "Falla no conexion con la Interface ";

                        if (CancelarProcesarTurno != null)
                        {
                            CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Cancelar Procesar Turno." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                    }
                }

                /////

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
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|PrecioNivel1: " +
                                Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);
                                SWRegistro.Flush();// Borrar solo para Inspección

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                   (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1); 


                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: Evento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
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
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|PrecioNivel1: " +
                                Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);
                                SWRegistro.Flush();// Borrar solo para Inspección

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                   (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);

                            
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: Evento_TurnoAbierto");
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

        public void Evento_TurnoCerrado(string Surtidores, string PuertoTerminal)
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


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
        {

            try
            {

                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|0|Evento|Recibido (Solicitar Lecturas por Surtidor). Surtidores: " + Surtidor);
                SWRegistro.Flush();

                CondicionCiclo2 = false; //detiene las encuenstas DCF 

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso1: CondicionCiclo2 = " + CondicionCiclo2);
                SWRegistro.Flush();

                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara      
                byte CaraLectura;

                if (!string.IsNullOrEmpty(Surtidor))
                {
                    //Organiza banderas de pedido de lecturas para la cara IMPAR
                    CaraLectura = Convert.ToByte(Convert.ToInt16(Surtidor) * 2 - 1);


                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura|...... " + CaraLectura);
                    SWRegistro.Flush();

                    //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                    CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                    if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                    {

                        if (EstructuraRedSurtidor[CaraTmp].Estado == EstadoCara.Espera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                        {
                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {


                                CaraEncuestada = CaraTmp;
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias                           


                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                while (!EncuentaFinalizada)
                                {

                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                }

                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                int i;
                                for (i = 0; i <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; i++)
                                {


                                    Lecturas += (Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[i].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        EstructuraRedSurtidor[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        EstructuraRedSurtidor[CaraTmp].ListaGrados[i].Lectura);
                                    SWRegistro.Flush();
                                }

                            }
                        }
                        else
                        {

                            if (EstructuraRedSurtidor[CaraTmp].Estado == EstadoCara.Indeterminado) //DCF 24_08_2014
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara En estado: " + EstructuraRedSurtidor[CaraTmp].Estado);
                                SWRegistro.Flush();

                                Lecturas = "E_ Estado Indeterminado";
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + EstructuraRedSurtidor[CaraTmp].Estado);
                                SWRegistro.Flush();

                                Lecturas = "E_ Manguera levantada";
                            }

                           
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                        SWRegistro.Flush();

                        Lecturas = "E_ Cara fuera de red";
                    }




                    //Organiza banderas de pedido de lecturas para la cara PAR
                    CaraLectura = Convert.ToByte(Convert.ToInt16(Surtidor) * 2);

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|CaraLectura|...... " + CaraLectura);
                    SWRegistro.Flush();


                    //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                    CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                    if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                    {
                        if (EstructuraRedSurtidor[CaraTmp].Estado == EstadoCara.Espera)//si esta en reposo envi el proceso de lecturas por surtidor                   
                        {

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraTmp].Activa)
                            {

                                CaraEncuestada = CaraTmp;
                                CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias


                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura por Surtidor ");
                                SWRegistro.Flush();

                                while (!EncuentaFinalizada)
                                {
                                    //Espera que se libere el proceso en : if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                }

                                TomarLecturas(); // obtener las lecturas de la cara en cuestion 

                                int i;
                                for (i = 0; i <= EstructuraRedSurtidor[CaraTmp].ListaGrados.Count - 1; i++)
                                {
                                    Lecturas += (Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[i].MangueraBD) + "|" +
                                    Convert.ToString(EstructuraRedSurtidor[CaraTmp].ListaGrados[i].Lectura) + "|");

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Reporta lecturas Por Surtidor. Manguera " +
                                        EstructuraRedSurtidor[CaraTmp].ListaGrados[i].MangueraBD + " - Lectura " +
                                        EstructuraRedSurtidor[CaraTmp].ListaGrados[i].Lectura);
                                    SWRegistro.Flush();
                                }

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Inconsistencia|Cara No esta en Reposo. Estado: " + EstructuraRedSurtidor[CaraTmp].Estado);
                            SWRegistro.Flush();

                            Lecturas = "E_ Manguera levantadas";
                        }

                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: SolicitarLecturasSurtidor");
                        SWRegistro.Flush();

                        Lecturas = "E_ Cara fuera de red";
                    }
                }



                CondicionCiclo2 = true; //Activa las encuenstas DCF 

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso: CondicionCiclo2 = " + CondicionCiclo2);
                SWRegistro.Flush();

            }
            catch (Exception Excepcion)
            {

                CondicionCiclo2 = true; //Activa las encuenstas DCF 

                string MensajeExcepcion = "Excepcion en el Metodo Lectura_Surtidor: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }




        #endregion
    }
}


        #endregion