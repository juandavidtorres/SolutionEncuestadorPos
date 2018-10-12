
using System;
using System.Collections.Generic;
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using System.Net.Sockets;
using System.Net;


//using gasolutions.Factory;
namespace POSstation.Protocolos
{
    public class Bennett : iProtocolo
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

        byte CRC;

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

        string Valor_Programado = "0000"; //para la predeterminacion

        public enum ComandoSurtidor
        {
            Estado = 0x0F,//OK
            Autorizar = 0x30,//OK        
            Totales = 0x00,//OK
            ParcialDespacho = 0x01, //REGRESA TAMBIEN EL STATUS !!!
            Desautorizar = 0x03,
            TotalDespacho = 0x11,
            CambiarPrecio = 0x1B,
            Precio_Despacho= 0x2E,
            Activa_predeterminacion = 0x09, 
            PredeterminarVentaDinero = 0x05,
            PredeterminarVentaVolumen = 0X06,  //solo para datos enteros 
            PredeterminarVentaVolumen1 = 0x3B,
            Desactiva_predeterminacion =0x0A,

            //Consulta_Preset = 0x36,

 
         
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

        TcpClient ClienteBennett;


        NetworkStream Stream;

        //byte[] TramaRxTemporal = new byte[250];
        int BytesRecibidos = 0;


        //Bennet
        

        public Bennett(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                this.Puerto = Puerto;

                AplicaServicioWindows = true;
                //this.AplicaTramas = AplicaTramas;                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion

                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 4800;//pruebas zigbee 9600
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
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
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;


                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 76/08/2015 - 2427");  //Para varias Mangueras 21-04-2015     
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 18/08/2015 - 1939");  //PPredeterminacion vol entero y envia 999 en cara en reposo      
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 19/08/2015 - 0942");  //Desactivacion de Bit_s: Clear 0A y 03
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 04/09/2015 - 1037"); //case (0x48): //estado 48 se encuentra autorizado con manguera en reposo 04_09_2015
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 28/03/2017 - 4050"); //Multi Grados - cambio en la predeterminacion por volumen -- conver a importe para la serie pacific
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 24/04/2017 - 1337"); //24/04/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 03/05/2017 - 1700");//DCF 03/05/2017
                 //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 05/05/2017 - 1549");//DCF 05/05/2017
                 //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 10/05/2017 - 1826"); //DCF 10/05/2017
                 //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 15/05/2017 - 2117"); //DCF 15/05/2017
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 15/05/2017 - 2148"); //DCF 15/05/2017  + teimpo de espera para reducir las perdidas de datos. 
                 //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 17/05/2017 - 1808");//DCF 17/05/2017  
                 SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 08/03/2018 - 1607");//DCF Archivos .txt 08/03/2018  
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
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase Bennett: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        public Bennett(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Sucesos(" + Puerto + ").txt";
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
                        ClienteBennett = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteBennett.GetStream();

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
                    PuertoCom.BaudRate = 5760;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
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
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);



                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett TCP_IP. 07.08.2015-2426");  //Grad 0 una sola manguera por lado. 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 18/08/2015 - 1939");  //PPredeterminacion vol entero y envia 999 en cara en reposo 
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 19/08/2015 - 0942");  //Desactivacion de Bit_s: Clear 0A y 03
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 04/09/2015 - 1037"); //case (0x48): //estado 48 se encuentra autorizado con manguera en reposo 04_09_2015
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 28/03/2017 - 4050"); //Multi Grados - cambio en la predeterminacion por volumen -- conver a importe para la serie pacific
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 24/04/2017 - 1337"); //24/04/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 03/05/2017 - 1700");//DCF 03/05/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 05/05/2017 - 1549");//DCF 05/05/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 10/05/2017 - 1826"); //DCF 10/05/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 15/05/2017 - 2117"); //DCF 15/05/2017
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 15/05/2017 - 2148"); //DCF 15/05/2017  + teimpo de espera para reducir las perdidas de datos. 
               // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 17/05/2017 - 1808");//DCF 17/05/2017  
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo Bennett. Modificado 08/03/2018 - 1607");//DCF Archivos .txt 08/03/2018  
               
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
                           + " - MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraEncuestada2].MultiplicadorPrecioVenta);

                    SWRegistro.Flush();
                }


                //Ciclo Infinito
                while (CondicionCiclo)
                {

                    VerifySizeFile();

                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {
                        //Si la cara está activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {
                            CaraEncuestada = ORedCaras.Cara;//Cara Asignado 
                         
                            CaraID = EstructuraRedSurtidor[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias
                           

                            //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                            if (ProcesoEnvioComando(ComandoSurtidor.Estado, true))
                                TomarAccion();
                        }

                        Thread.Sleep(20);

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
        public bool 
            
            ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar, bool PrecioNivel1)
        {
            try
            {
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
                    ArmarTramaTx(ComandoaEnviar, PrecioNivel1);// DCF OJO   en caso de falla cargar el # de BytesEsperados = 19;

                    if (EsTCPIP)
                        EnviarComando_TCPIP();

                    else
                        EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor


                    if (BytesEsperados > 0)
                    {

                        if (EsTCPIP)
                            RecibirInformacion_TCPIP();
                        else
                            RecibirInformacion();

                        Reintentos += 1;
                    }
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

                byte Cara_TX = 0; 
                switch (CaraEncuestada)
                {
                    case 1:
                        Cara_TX = 0x00;
                        break;
                    case 2:
                        Cara_TX = 0x40;
                        break;
                    case 3:
                        Cara_TX = 0x80;
                        break;
                    case 4:
                        Cara_TX = 0xc0;
                        break;
                }


                ComandoCaras = ComandoTx;
              

                
                int Command = Cara_TX | (byte)ComandoTx;


                if (ComandoTx == ComandoSurtidor.TotalDespacho)
                {
                    Command = Cara_TX | (byte)0x01;//Se envia el Send Display Data & Status (DPRD) 
                }


                byte lengthTX = 0;


                switch (ComandoTx)
                {


                    case (ComandoSurtidor.Estado):
                        TramaTx = new byte[4] { 0xAA, (byte)Command, lengthTX, 0x00 }; // AA 0F 00 F1

                         BytesEsperados = 7;
                         TimeOut = 250;//200;

                        break;

                    //Totalizador AA 00 02 00 00 FE
                    case(ComandoSurtidor.Totales):

                        if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorAutorizar)
                            EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado; //DCF 03/05/2017

                         if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho ||
                             EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho || 
                             EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado)
                               EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoVenta; //DCF 15/05/2017



                        TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x00,(byte)EstructuraRedSurtidor[CaraEncuestada].GradoCara, 0x00 }; // AA 0F 00 F1

                         BytesEsperados = 13;
                         TimeOut = 350;//300;
                        break;


                    // hose one authorize : AA 30 02 06 01 C7
                    case (ComandoSurtidor.Autorizar):

                        //TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x06, 0x01, 0x00 }; // AA 0F 00 F1  hose one authorize : AA 30 02 06 01 C7
                        //(TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x06, 0x00, 0x00 }; // hose all authorize 
                           TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x06, 0x00, 0x00 }; // hose all authorize  Multi Grados - cambio en la predeterminacion por volumen -- conver a importe para la serie pacific


                         BytesEsperados = 4;
                         TimeOut = 250;//200;
                        break;

                    case (ComandoSurtidor.ParcialDespacho): //AA 01 00 FF
                        
                         TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 }; // Confirmar si regresa los parciales 

                         BytesEsperados = 19;
                         TimeOut = 300;//250;

                        break;

                    case (ComandoSurtidor.CambiarPrecio)://1B Write Tier 1 PPV A : AA 1B 02 11 23 AF ---> Cambio de precio OK 

                        int GradoP = (EstructuraRedSurtidor[CaraEncuestada].GradoCara); //Multi Grados - cambio en la predeterminacion por volumen -- conver a importe para la serie pacific

                    switch (GradoP)
                    { 

                    case 0x00:
                              Command = Cara_TX | 0x1B; //Write Tier 1 PPV A (PAOWR)
                        break;

                    case 0x01:
                        Command = Cara_TX | 0x1C; //  Write Tier 1 PPV B (PBOWR) - MPII1.0
                        break;

                    case 0x02:
                        Command = Cara_TX | 0x1D; //Write Tier 1 PPV D (PDOWR)
                        break;

                    case 0x03:
                        Command = Cara_TX | 0x1E; //Write Tier 1 PPV C
                        break; 
                     }

                      TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x00, 0x00, 0x00 };
                        

                       string strPrecio = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoCara].PrecioNivel1 *
                                        EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).PadLeft(6, '0');


                       TramaTx[4] = Convert.ToByte(strPrecio.Substring(strPrecio.Length - 4, 2), 16);
                       TramaTx[3] = Convert.ToByte(strPrecio.Substring(strPrecio.Length - 2, 2), 16);
                           
                           

                        BytesEsperados = 4;
                         TimeOut = 300;//200;

                        break;


                    case (ComandoSurtidor.TotalDespacho): //AA 01 00 FF

                        TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 }; // $Importe y Volumen de la venta actual en pantalla 

                        BytesEsperados = 19;
                        TimeOut = 300;//200;

                        break;
                        
                    case (ComandoSurtidor.Precio_Despacho): //AA 2E 02 00 00 D0 

                                //2E Send PPV Tier 1 - Price 1 : AA 2E 02 01 00 CF
                                //2E Send PPV Tier 1 - Price 2 : AA 2E 02 01 01 CE
                                //2E Send PPV Tier 1 - Price 3 : AA 2E 02 01 02 CD
                                //2E Send PPV Tier 1 - Price 4 : AA 2E 02 01 03 CC                                             

                          if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.PorAutorizar)
                                EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado; //DCF 03/05/2017

                          //DCF 10/05/2017
                         if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho ||
                             EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespacho || 
                             EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.FinDespachoForzado)
                               EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoVenta; //DCF 15/05/2017


                         int GradoPPV = (EstructuraRedSurtidor[CaraEncuestada].GradoCara);


                        TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x00, (byte)GradoPPV, 0x00 }; //PV: $ 

                        BytesEsperados = 8;
                        TimeOut =250;// 200;

                        break;


                    case ComandoSurtidor.Activa_predeterminacion:

                        TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 };
                        BytesEsperados = 4;
                        TimeOut = 150;

                        break; 


                    case( ComandoSurtidor.PredeterminarVentaDinero): // AA 05 03 00 99 99 C6

                        
                        TramaTx = new byte[7] { 0xAA, (byte)Command, 0x03, 0x00, 0x00, 0x00, 0x00 }; //PV: $ 


                         Valor_Programado = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado)).PadLeft(6, '0');

                        TramaTx[5] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 6, 2), 16);
                        TramaTx[4] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 4, 2), 16);
                        TramaTx[3] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 2, 2), 16);

                        BytesEsperados = 4;
                        TimeOut = 200;

                        break; 

                    case ComandoSurtidor.PredeterminarVentaVolumen:

                         
                      TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x00, 0x00, 0x00 }; //PV: Vol  no funciono para chinauta

                    
                         Valor_Programado = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado)).PadLeft(4, '0');

                        TramaTx[4] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 4, 2), 16);
                        TramaTx[3] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 2, 2), 16);

                        BytesEsperados = 4;
                        TimeOut = 200;

                        break;




                    case ComandoSurtidor.PredeterminarVentaVolumen1:


                        //  TramaTx = new byte[6] { 0xAA, (byte)Command, 0x02, 0x00, 0x00, 0x00 }; //PV: Vol  no funciono para chinauta

                        //Code           3BH

                        //Message        SYNC
                        //3BH
                        //04H
                        //Prepay Volume  00000.0XX
                        //Preset Volume  0000X.X00
                        //Preset Volume  00XX0.000
                        //Preset Volume  XX000.000
                        //CHECKSUM

                        TramaTx = new byte[8] { 0xAA, (byte)Command, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00 };

                        Valor_Programado = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado)).PadLeft(8, '0');

                        TramaTx[6] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 8, 2), 16);
                        TramaTx[5] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 6, 2), 16);
                        TramaTx[4] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 4, 2), 16);
                        TramaTx[3] = Convert.ToByte(Valor_Programado.Substring(Valor_Programado.Length - 2, 2), 16);

                        BytesEsperados = 4;
                        TimeOut = 200;

                        break;



                    case ComandoSurtidor.Desactiva_predeterminacion: //0A Clear Prepay Bit : AA 0A 00 F6
                        TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 };
                        BytesEsperados = 4;
                        TimeOut = 150;

                        break;



                    //case ComandoSurtidor.Consulta_Preset:
                    //    TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 };
                    //    BytesEsperados = 4;
                    //    TimeOut = 150;

                    //    break;



                    case ComandoSurtidor.Desautorizar: //03 Clear ARM : AA 03 00 FD --- > desautorizar la venta anterior y el despacho siguiente 
                        TramaTx = new byte[4] { 0xAA, (byte)Command, 0x00, 0x00 };
                        BytesEsperados = 4;
                        TimeOut = 200;//150;
                        break; 



                    default: // se envia estado
                        TramaTx = new byte[4] { 0xAA, (byte)Command, lengthTX, 0x00 }; // AA 0F 00 F1
                         BytesEsperados = 7;
                         TimeOut = 200;

                        break; 


                
                }


                CalcularChecksum(TramaTx);

                TramaTx[TramaTx.Length - 1] = CRC;

                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                eco = Convert.ToByte(TramaTx.Length);

                       





            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo ArmarTramaTx: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Comando " + ComandoTx + ":" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        private byte CalcularChecksum(byte[] Trama)
        {
            try
            {
                CRC = new byte();
                for (int i = 1; i <= Trama.Length - 2; i++)
                {
                    CRC += Trama[i];
                }
                int residuo;


                residuo = CRC % 256;
                CRC = (byte)(256 - residuo);

                return Convert.ToByte(CRC);
            }
            catch (Exception Excepcion)
            {

                return 1;
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
                    "|" + CaraID + "|Tx|" + strTrama);

                SWTramas.Flush();




                //Thread.Sleep(TimeOut);//Tiempo 
                Thread.Sleep(TimeOut);//Tiempo solo para pruebas de loop 
                Thread.Sleep(100);//Se suman 100 ms mas para mejora la conexion con las antenas wifi y son  un solo surtidor por red 24/04/2017
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
                if (ClienteBennett == null)
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
                            ClienteBennett = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CaraID + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteBennett == null)
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

                        if (ClienteBennett != null)
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
                if (!this.ClienteBennett.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteBennett.Client.BeginDisconnect(true, callBack, ClienteBennett);

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



                while (!this.ClienteBennett.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteBennett.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteBennett);
                        //ClienteGilbarco.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteBennett.Client.Connected)
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
                this.Stream = ClienteBennett.GetStream();
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
                ClienteBennett.Close();
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
                ClienteBennett = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteBennett.GetStream();
                if (this.ClienteBennett.Client.Connected == true)
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
                if (!TramaEco)
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = BytesEsperados + eco;
                BytesEsperados_Extended = BytesEsperados_Extended + eco;


                byte[] TramaRxTemporal = new byte[BytesEsperados_Extended];


                if (Stream == null)
                {
                    ErrorComunicacion = true;
                    return;
                }

                if (!Stream.DataAvailable)
                    Thread.Sleep(40);



                if (Stream.DataAvailable)
                {
                    
                        TramaRxTemporal = new byte[BytesEsperados];

                    // Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            Bytes_leidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }



                  
                        //LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                        ErrorComunicacion = false;


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
                        //LOGUEO DE TRAMA  
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

                        //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes_leidos == BytesEsperados)
                    {
                        AnalizarTrama();
                    }
                    else if (ErrorComunicacion == false)
                    {

                        SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        SWRegistro.Flush();

                        ErrorComunicacion = true;                        
                    }

                    //SWRegistro.Flush();
                }
                else if (ErrorComunicacion == false)
                {
                    ErrorComunicacion = true;
                }


                Thread.Sleep(50);//Se suman 50 ms mas para dar tiempo de recuoeracion luego de RX y para el nuevo TX y  24/04/2017

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
                ClienteBennett.Client.Close();
                ClienteBennett.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteBennett = null;
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

                    if (Bytes_leidos > 0)
                    {
                        if (!TramaEco)
                            eco = 0;

                        //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                        BytesEsperados = BytesEsperados + eco;

                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                        PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length - eco];

                        string strTrama = "";
                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                        {
                            TramaRx[i] = TramaTemporal[i + eco];
                            strTrama += TramaRx[i].ToString("X2") + "|";
                        }

                        ///////////////////////////////////////////////////////////////////////////////
                        //LOGUEO DE TRAMA RECIBIDA

                        if (this.AplicaServicioTramas)
                        {
                            SWTramas.WriteLine(
                                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                "|" + CaraID + "|Rx|" + strTrama + "|#| " + TramaRx.Length);

                            SWTramas.Flush();

                        }


                        if (Bytes_leidos == BytesEsperados)
                        {
                            AnalizarTrama();

                            ErrorComunicacion = false;
                        }
                        else if (ErrorComunicacion == false)
                        {
                            ErrorComunicacion = true;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error Bytes_leidos = " + Bytes_leidos + " - BytesEsperados =" + BytesEsperados);
                            SWRegistro.Flush();
                        }

                    }

                    else
                    {

                        ErrorComunicacion = true;
                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error de comunicacion surtidor no responde");
                        //SWRegistro.Flush();


                    }
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

                    case(ComandoSurtidor.Precio_Despacho):
                        RecuperarPrecio_Despacho();
                        break;

                    case ComandoSurtidor.Desautorizar:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cara Desautorizar: ");
                            SWRegistro.Flush();
                            break;

                    case ComandoSurtidor.Desactiva_predeterminacion:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento| Informa desactivación de predeterminación por sistema: ");
                            SWRegistro.Flush();

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
                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;



                //Se separan el Codigo del estado y la cara en variables diferentes.  La "e" es el parametro aditivo del ECO recibido
                int CodigoEstado0 = Convert.ToByte(TramaRx[TramaRx.Length - 4]);
                int CodigoEstado1 = Convert.ToByte(TramaRx[TramaRx.Length - 3]);
                int CodigoEstado2 = Convert.ToByte(TramaRx[TramaRx.Length-2]);


                //codigoEstado0 entrega: 
                if (EstructuraRedSurtidor[CaraEncuestada].Status0 != CodigoEstado0) //Manguera 27 03 2017
                {
                    EstructuraRedSurtidor[CaraEncuestada].Status0 = CodigoEstado0;
                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Codigo Status0 = |" + CodigoEstado0.ToString("X2"));
                    //SWRegistro.Flush();
                }


             if (EstructuraRedSurtidor[CaraEncuestada].Status1 != CodigoEstado1)
             {
                 EstructuraRedSurtidor[CaraEncuestada].Status1 = CodigoEstado1;
                 //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Codigo Status1 = |" + CodigoEstado1.ToString("X2"));
                 //SWRegistro.Flush();
             }

             if (EstructuraRedSurtidor[CaraEncuestada].Status2 != CodigoEstado2)
             {
                 EstructuraRedSurtidor[CaraEncuestada].Status2 = CodigoEstado2;
                 //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Codigo Status2 = |" + CodigoEstado2.ToString("X2"));
                 //SWRegistro.Flush();
             }



             if ((CodigoEstado2 & 0x02) == 0x02)//|| (CodigoEstado0 & 0x02) == 0x00 || (CodigoEstado0 & 0x04) == 0x00 || (CodigoEstado0 & 0x80 ) == 0x00)// case (0x40):
             {
                // EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Error;
                 SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + "EstadoCara.Error********");
                 SWRegistro.Flush();

             }



                byte CaraqueResponde = Convert.ToByte(TramaRx[1] & (0xc0));// revisar ?????

                switch(CaraqueResponde)
                {
                    case 0x00:
                        CaraqueResponde = 1;
                        break;

                    case 0x40:
                        CaraqueResponde = 2;
                        break;

                    case 0x80:
                        CaraqueResponde = 3;
                        break;

                    case 0xC0:
                        CaraqueResponde = 4;
                        break;
                }
              
                //Evalua si la informacion que se recibio como respuesta corresponde a la cara que fue encuestada
                if ((CaraqueResponde) == CaraEncuestada) //rEVISAR ??????????????????????????????????????*************************************************************************************
                {
                    InconsistenciaDatosRx = false; //No hubo error por fallas en datos                                   


           #region Status 0       
       
                        #region Estado Espera
                        //       Grado 0 Reposo                         Grado 1 Reposo                   Grado 2  Reposo                 Grado 3 Reposo
                        //if ((CodigoEstado0 & 0x01) == 0x00)//|| (CodigoEstado0 & 0x02) == 0x00 || (CodigoEstado0 & 0x04) == 0x00 || (CodigoEstado0 & 0x80 ) == 0x00)// case (0x40):
                    
                    if ((CodigoEstado0 & 0X87) == 0x00)   //DCF 15/05/2017 
                    {
                            if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true)
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                                //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Finaliza venta en Estado Espera");
                                //SWRegistro.Flush();
                            }
                            else
                            {
                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;
                            }



                            Estado_Actual(CodigoEstado0); //return;//Salga DCF 15/05/2017
                            return;
                        }
                        #endregion

                        #region Estado Por Autorizar o Manguera ON //Fin de venta Manguera off
                        //   (0x61)  Grado 0 Reposo               (0x62) Grado 1 Reposo          (0x64)  Grado 2  Reposo             (0xC0) Grado 3 Reposo // 
                       if ((CodigoEstado0 & 0x87) != 0x00)// || (CodigoEstado0 & 0x02) == 0x02 || (CodigoEstado0 & 0x04) == 0x04 || (CodigoEstado0 & 0x80) == 0x80)                    
                        {
                           // if ((EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true) && ((CodigoEstado0 & 0x10) == 0x00))
                            
                           //if(EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == true) //DCF 15/05/2017
                           // {
                           //     EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                           //     SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Venta detenida. Estado por Autorizar SE ENVIA Estado = EstadoCara.FinDespachoForzado");
                           //     SWRegistro.Flush();
                           // }
                            //else                           

                                EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;

                                //x087 = 10000111  Ver manual status0 pag 6 
                                if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)//DCF 15/05/2017
                                      {
                                            switch (CodigoEstado0 & 0x87 )
                                            {

                                                case 0X01:
                                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 0;
                                                    break;

                                                case 0X02:
                                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 1;
                                                    break;

                                                case 0X04:
                                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 2;
                                                    break;

                                                case 0X80:
                                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = 3;
                                                    break;
                                            }
                                    }
                            
                        }

                        #endregion

                        #region Estado Autorizado
                        // break;
                       //if (((CodigoEstado0 & 0x8) == 0x08) && ((CodigoEstado0 & 0x01) == 0x01)) //case (0x48): //estado 48 se encuentra autorizado con manguera en reposo 04_09_2015
                        if ((CodigoEstado0 & 0x8) == 0x08)  //DCF 10/05/2017
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Autorizado;
                        // break;

                        #endregion

                        #region Estado en Despacho
                        if ((CodigoEstado0 & 0x10) == 0x10) //case (0x59):
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;


                            switch (CodigoEstado0 & 0x87)//DCF 10/05/2017
                            {

                                case 0X01:
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta = 0;
                                    break;

                                case 0X02:
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta = 1;
                                    break;

                                case 0X04:
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta = 2;
                                    break;

                                case 0X80:
                                    EstructuraRedSurtidor[CaraEncuestada].GradoVenta = 3;
                                    break;
                            }
                        }

                        #endregion

                    }

           #endregion



                Estado_Actual(CodigoEstado0);//DCF 15/05/2017


                    //#region Estado en Fin de Despacho en ventas predeterminadas o con manguera sin colgar  (0x00)
                //    //Bit 5 (0x10)venta On/Off 
                //  if (((CodigoEstado0 & 0x10) == 0x00) && ((CodigoEstado0 & 0x01) == 0x00))// || (CodigoEstado0 & 0x02) == 0x02 || (CodigoEstado0 & 0x04) == 0x04 || (CodigoEstado0 & 0x80 ) == 0x80))///
                //  {
                //      EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespacho;
                //      SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|Finaliza venta predeterminada");
                //      SWRegistro.Flush();
                //  }
               // #endregion

                

                    ////Almacena en archivo el estado actual del surtidor
                    //if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    //{
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado +
                    //        " - " + CodigoEstado0.ToString("X2").PadLeft(2, '0'));
                    //    SWRegistro.Flush();
                    //}
                ////}
                //else
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Comando: " + ComandoCaras + " - Cara que Responde: " + CaraqueResponde);
                //    SWRegistro.Flush();
                //    InconsistenciaDatosRx = true;
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo AsignarEstado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public void Estado_Actual(int CodigoEstado0)//DCF 15/05/2017
        {
            //Almacena en archivo el estado actual del surtidor
            if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado +
                    " - " + CodigoEstado0.ToString("X2").PadLeft(2, '0'));
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

                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    /***************************ESTADO EN ESPERA***************************/
                    #region Estado en Espera
                    case (EstadoCara.Espera):

                        //Informa cambio de estado
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado) //DCF 10/05/2017
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

                            //Programar el volumen maximo en 999 para ventas siguientes
                            EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado = 0;
                            Predeterminar();

                            //Desactivacion de Bit_s: Clear 0A y 03
                            //DesAutorizacion para que no despache en la siguiente venta en caso que no entregara combustible en la venta actual 
                            //Predeterminacion de sistema para que puedan programen desde el surtidor 

                            //0A Clear Prepay Bit : AA 0A 00 F6
                            ProcesoEnvioComando(ComandoSurtidor.Desactiva_predeterminacion, false);


                            //03 Clear ARM : AA 03 00 FD --- > desautorizar
                            ProcesoEnvioComando(ComandoSurtidor.Desautorizar, false);

                        }


                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;


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
                        ArmarTramaTx(ComandoSurtidor.ParcialDespacho, false);//******************************************************************

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
                            ProcesoEnvioComando(ComandoSurtidor.Desautorizar, false);
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

                   /***************************ESTADO EN FIN DE DESPACHO FORZADO***************************/
                    #region Estados Fin de Despacho

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
                         {
                            //Thread HiloVenta = new Thread(ProcesoFindeVenta);
                            //HiloVenta.Start();
                            ProcesoFindeVenta();

                            //EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;//DCF 05/05/2017
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
                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Envia comando Totales para desbloquear");
                        //SWRegistro.Flush();
                        //ProcesoEnvioComando(ComandoSurtidor.Totales, false);
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

                          
                            #region _Extended -- estado = 7 CALL
                           
                                Reintentos = 0;
                                do
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


                                    Reintentos++;
                                } while (Reintentos <= 3);

                            
                            #endregion;

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


                                string strLecturasVolumen = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");

                                if (AplicaServicioWindows)
                                {
                                    if (LecturaInicialVenta != null)
                                    {

                                        LecturaInicialVenta(CaraID, strLecturasVolumen);

                                        //Loguea Evento de envio de lectura
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Venta: " +
                                            strLecturasVolumen);
                                        SWRegistro.Flush();
                                    }
                                }
                              

                              

                                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioSurtidorNivel1;


                                //Si la siguiente venta es predeterminada, realiza el proceso de programación
                               // if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado >= 0)
                                if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado > 0) //dcf 27 03 2017
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
                                        EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Despacho &&
                                        EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.Error
                                        && (Reintentos <= 3));

                                    //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                                    if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                        EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)
                                    {
                                        EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
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
               // string GuidVenta = data[4];
                string GuidVenta = "0";

                if (AplicaServicioWindows)
                {
                    if (AutorizacionRequerida != null)
                    {
                       
                        AutorizacionRequerida(CaraTmp, IdProducto, IdManguera, Lectura, GuidVenta);
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

                //int Reintentos = 0;
                decimal VolumenCalculado = new decimal();


                //rECUPERAR PRECIO DE VENTA

               // if(ProcesoEnvioComando(ComandoSurtidor.CambiarPrecio, false))

                if(ProcesoEnvioComando(ComandoSurtidor.Precio_Despacho, false))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Obtiene Precio de Venta: " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                    SWRegistro.Flush();
                }
                else
                {

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error| NO Obtovo Precio de Venta: " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                    SWRegistro.Flush();                
                }


                //Obtiene los Valores Finales de la Venta (Pesos y Metros cubicos despachados)
                if (ProcesoEnvioComando(ComandoSurtidor.TotalDespacho, false))
                {
                    //Si el grado que responde está dentro del la lista de grados
                    if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >= EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura Final de Venta en el Grado: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                        SWRegistro.Flush();// escribir el grado que realizo la venta DCF 29-10-2013

                        //Obtiene la Lectura Final de la Venta
                       // EstructuraRedSurtidor[CaraEncuestada].GradoCara = EstructuraRedSurtidor[CaraEncuestada].GradoVenta; //rECUPERAR
                         // EstructuraRedSurtidor[CaraEncuestada].GradoVenta = EstructuraRedSurtidor[CaraEncuestada].GradoCara; //DCF 10/05/2017

                        //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                        if (EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado != EstructuraRedSurtidor[CaraEncuestada].GradoVenta)
                        {
                            /*- WBC: Modificado el 10/07/2009 ---*/
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado +
                                " - Grado que vendio: " + EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                            SWRegistro.Flush();

                        }

                    

                        TomarLecturas();

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
                        //if (VolumenCalculado >= 0 && EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta >0)
                        if (VolumenCalculado > 0 && EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].LecturaInicialVenta > 0)//DCF 10/05/2017
                      
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

                        //Si se realizó una venta con valores de m3 y $ mayor que cero
                        if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0)
                        {
                            //YEZID
                            //Aqui va el lanzamiento del evento de fin de venta
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                            ////Calcula la correspondencia entre el Volumen, el Precio y el Importe
                            //decimal TotalVentaCalculada = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                            //    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|MultiplicadorPrecioVenta = " + EstructuraRedSurtidor[CaraEncuestada].MultiplicadorPrecioVenta); //Borra
                            SWRegistro.Flush(); //Borrar solo para Prueba

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
                

                  int LRCCalculado = CalcularChecksum(TramaRx);

                int LRCObtenidoEnTrama = TramaRx[(TramaRx.Length - 1)];
                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTrama == LRCCalculado)//Eco
                {
                   // byte CaraqueResponde = Convert.ToByte(TramaRx[1] & (0xc0) +1);// revisar ?????

                    byte CaraqueResponde = Convert.ToByte(TramaRx[1] & (0xc0));// revisar ?????

                    switch (CaraqueResponde)
                    {
                        case 0x00:
                            CaraqueResponde = 1;
                            break;

                        case 0x40:
                            CaraqueResponde = 2;
                            break;

                        case 0x80:
                            CaraqueResponde = 3;
                            break;

                        case 0xC0:
                            CaraqueResponde = 4;
                            break;
                    }
              



                    if (CaraqueResponde == CaraEncuestada)
                    {

                            ////Se obtiene el Precio con que se realizo la venta
                            //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                            //    ObtenerValor(2,2) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;




                            EstructuraRedSurtidor[CaraEncuestada].TotalVenta = (ObtenerValor(3, 8) / 100000) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                            EstructuraRedSurtidor[CaraEncuestada].Volumen = (ObtenerValor(9, 14) / 10000) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;
                

                            //Se obtiene el Dinero despachado
                       //Se Optiene el grado por donde se despacho
                           // EstructuraRedSurtidor[CaraEncuestada].GradoVenta = Convert.ToByte(0x0F & TramaRx[9]);
                            //if (EstructuraRedSurtidor[CaraEncuestada].GradoVenta != EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado)
                            //{
                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado Autorizado_Extended: " +
                            //        EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " difiere de grado que reporta fin de venta: " +
                            //        EstructuraRedSurtidor[CaraEncuestada].GradoVenta);
                            //    SWRegistro.Flush();
                            //}
                        
                        
                        
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
                        " - LRC Calculado: " + LRCCalculado);
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
               // TomarLecturas(); //DCF 17/05/2017 

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                //Cambia el precio si es apertura de turno
                if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                    SWRegistro.Flush();
                    CambiarPrecios(EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count * 2);
                }

                int i;
                for (i = 0; i <= EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1; i++)
                {

                    EstructuraRedSurtidor[CaraEncuestada].GradoCara = i; //DCF 17/05/2017    
                    TomarLecturas(); //DCF 17/05/2017 


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

                //Calcula el LRC
                int LRCCalculado = CalcularChecksum(TramaRx);

                int LRCObtenidoEnTrama = TramaRx[(TramaRx.Length - 1)];
                //Si el LRC Recibido (TramaRx[TramaRx.Length - 2] AND 0x0F) es igual al calculado
                if (LRCObtenidoEnTrama == LRCCalculado)//Eco
                {
                    //Obtiene todos los valores de cada uno de los Grados que tiene el surtidor
                    int i = 0;

                    
                
                        //Obtiene todos los valores de Totalizadores y Precios de cada uno de los Grados que tiene el surtidor
                       
                            try
                            {
                                //Obtiene el grado de la trama recibida
                                GradoSurtidor = Convert.ToByte(TramaRx[4]);
                                if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1 >= GradoSurtidor)
                                {
                                    //Obtiene las lecturas de volumen en el grado respectivo                        
                                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[GradoSurtidor].Lectura =
                                        (ObtenerValor(5,11) / (EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador * 10000));
                                }
                                else
                                {
                                    //Si la cantidad de grados a encuestar es menor que el grado recibido del surtidor, se sale
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|GradoSurtidor = " +
                                    GradoSurtidor + " - ListaGrados: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count);
                                    SWRegistro.Flush();//DCF 03/05/2017
                                    
                                }
                            }
                            catch (Exception ex)
                            {
                                string MensajeExcepcion = "Excepcion en el Metodo RecuperarTotalizadores_Extended 1: " + ex;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                                SWRegistro.Flush();
                            }
                            //Ciclo realizado hasta que encuentre el final de la trama 0xFB
                        

                    
        #endregion;


                    InconsistenciaDatosRx = false;
                }
                else
                {
                    InconsistenciaDatosRx = true;
                    SWRegistro.WriteLine(DateTime.Now + "" + CaraID + "|Inconsistencia|Comando " + ComandoCaras + " responde LRC Errado. LRC Obtenido: " +
                       LRCObtenidoEnTrama + " - LRC Calculado: " + LRCCalculado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo RecuperarTotalizadores 2: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        public void RecuperarPrecio_Despacho()
        {

            ////Se obtiene el Precio con que se realizo la venta
            EstructuraRedSurtidor[CaraEncuestada].PrecioVenta =
                ObtenerValor(5, 6) / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;


        }

        //OBTIENE EL VALOR EN PESOS DE LA VENTA EN CURSO Y CALCULA A PARTIR DE ESTE Y EL PRECIO EL VALOR DE VOLUMEN
        public void RecuperarParcialesdeVenta()
        {
            try
            {
                if (EstructuraRedSurtidor[CaraEncuestada].PrecioVenta == 0) //Gilbarco_Extended. Modificado 16.11.2012-11265 //Si no se tiene precio de venta, se toma por defecto el precio nivel 1 del grado que se asume está despachando                
                {
                    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso| Precio Tomado del Configurador - PrecioNivel1 = " + EstructuraRedSurtidor[CaraEncuestada].PrecioVenta);
                    SWRegistro.Flush();
                }

             
                  
                    EstructuraRedSurtidor[CaraEncuestada].TotalVenta = (ObtenerValor(3,8)/100000 ) / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                    EstructuraRedSurtidor[CaraEncuestada].Volumen = (ObtenerValor(9,14)/10000) / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;
                
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
        public bool CambiarPrecios(int NumeroDePreciosACambiar) //Cambio de precio en la apertura de turno 
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

                               if( ProcesoEnvioComando(ComandoSurtidor.Precio_Despacho, false))
                               {
                                   EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 =
                                       EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                               }
                          
                            Reintentos += 1;

                            if (EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 ==
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1)
                            {
                                NumeroDePreciosACambiar -= 1;
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].CambioPrecio = true;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio de precio  OK en Nivel 1 Grado: " +
                                    EstructuraRedSurtidor[CaraEncuestada].GradoCara + " |Precio: " + EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1);

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
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[i].PrecioNivel1);

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


                //if (!EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen)
                //{
                //    // se actia la predeterminacion comando 0x09
                //    ProcesoEnvioComando(ComandoSurtidor.Activa_predeterminacion, true);

                //}

                if (EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado == 0)
                 {
                     EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado = 9999;

                     EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false;
                     EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen = false;

                     ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen, false);
                     SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Volumen Max Predeterminado: " +
                        EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                     SWRegistro.Flush();

                 }
                               

                if(EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                    {

                        // se actia la predeterminacion comando 0x09
                        ProcesoEnvioComando(ComandoSurtidor.Activa_predeterminacion, true);


                        Thread.Sleep(500);//Tiempo solo para pruebas de loop 

                        //Arma trama para predeterminar por volumen
                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaDinero, false);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Volumen Predeterminado (importe convertido): " +
                           EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();

                        EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte = false; //DCF

                    }
                    if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen) //Multi Grados - cambio en la predeterminacion por volumen -- conver a importe para la serie pacific
                    {

                       // EstructuraRedSurtidor[CaraEncuestada].PrecioVenta
                        //para predeterminacion por volumen  no se usa:       ProcesoEnvioComando(ComandoSurtidor.Activa_predeterminacion, true);

                        //obtener prtecio de grado
                        ProcesoEnvioComando(ComandoSurtidor.Precio_Despacho, true);
                        

                       decimal ValorPredeterminado  = EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado * (decimal) EstructuraRedSurtidor[CaraEncuestada].PrecioVenta;

                       EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado = Math.Round(ValorPredeterminado);

                       // ArmarTramaTx(ComandoSurtidor.PredeterminarVentaVolumen, false);


                        // se actia la predeterminacion comando 0x09
                        ProcesoEnvioComando(ComandoSurtidor.Activa_predeterminacion, true);

                        Thread.Sleep(500);//Tiempo solo para pruebas de loop 

                        ArmarTramaTx(ComandoSurtidor.PredeterminarVentaDinero, false);

                        EstructuraRedSurtidor[CaraEncuestada].PredeterminarVolumen = false;
                      
                       //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Volumen Predeterminado: " +
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Valor de Volumen Calculado(convertido a importe): " +                           
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
                    {
                        EnviarComando();
                        RecibirInformacion();
                    }

                    //Thread.Sleep(200);//Tiempo solo para pruebas de loop 

                    //ProcesoEnvioComando(ComandoSurtidor.Consulta_Preset, true);

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

      
        //#endregion

        public void VerifySizeFile()
        {
            try
            {
                //FileInfo 
                FileInfo FileInf = new FileInfo(ArchivoTramas);           //DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }

                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Bennett-Sucesos(" + Puerto + ").txt";
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

        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera DCF11/12/2017
        {

        }

        public decimal ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal Valor = new decimal();
                string Valo_string = "";
                       
               
                //for (int i = (TramaRx.Length - 2); i >= 5; i--)
                for (int i = PosicionFinal; i >= PosicionInicial; i--)
                {

                   Valo_string += (TramaRx[i]).ToString("x2");

                }

                Valor = Convert.ToDecimal(Valo_string);
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

                byte CaraTmp;

                CaraTmp = ConvertirCaraBD(Cara);
                if (EstructuraRedSurtidor.ContainsKey(CaraTmp))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada + " Precio: " + Precio);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[CaraTmp].AutorizarCara = true;

                    ///////////////////////////
                    //Sólo para pruebas
                    //TipoProgramacion = 1;
                    //EstructuraRedSurtidor[Cara].ValorPredeterminado = Convert.ToDecimal(0.1);
                    //////////////////////////



                    //Valor a programar
                    EstructuraRedSurtidor[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado); ///debe ser con , ojo

                    EstructuraRedSurtidor[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    EstructuraRedSurtidor[CaraTmp].MangueraProgramada = Convert.ToInt16(MangueraProgramada);

                    EstructuraRedSurtidor[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = false;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = true;

                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraTmp].PredeterminarImporte = true;
                            EstructuraRedSurtidor[CaraTmp].PredeterminarVolumen = false;

                        }
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


        public void Evento_TurnoAbierto(string Surtidores, string PuertoTerminal, System.Array Precios)
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


                                //EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                //    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2) /
                                //    EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 2011.03.15-1705
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
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|MultiplicadorPrecioVenta: " + EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta + " - PrecioNivel1: " +
                                Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1);
                                SWRegistro.Flush();// Borrar solo para Inspección

                                EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                   (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1) /
                                   EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel 2011.03.15-1705

                                //EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                //    (Grados[EstructuraRedSurtidor[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2) /
                                //    EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta; //DCF precio Terpel2011.03.15-1705
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




        #endregion
    }
}

