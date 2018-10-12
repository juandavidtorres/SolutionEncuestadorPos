using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using System.Net.Sockets;       //Para manejo de la comunicación TCP/IP
using System.IO.Ports;          //Para manejo de la comunicación RS232
using POSstation.Protocolos;
using System.Threading;
using System.Net;

namespace POSstation.Protocolos
{
    public class FAFNIR
    {
        #region DECLARACIÓN DE VARIABLES
        //private Socket clientSocket;
        //private NetworkStream networkStream;
        //private BackgroundWorker bwReceiver;
        //private IPEndPoint serverEP;
        //private string networkName;

        Dictionary<int, Tanque> TanquesVR;

        Boolean ExisteValorInventario = false;

        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;

        string Archivo;
        Boolean EnvioComando = false;
        //Variable que almacen la ruta y el nombre del archivo que guarda sucesos en el proceso
        string ArchivoRegistros;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWRegistros;

        //Definición del timer de encuesta
        System.Timers.Timer PollingTimer;

        //Controla la comunicacion entre las aplicaciones por medio de eventos
        SharedEventsFuelStation.CMensaje oEventos;


        SerialPort PuertoFAFNIR = new SerialPort();
        TcpClient ClienteVeederRoot;
        NetworkStream Stream;
        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        private enum Comando_Medidor_fafnir
        {
            Inventario,
            set_Time,
            set_Day,
            Volume_Producto,
            TC_volumen,
            Ullage,
            Height_Producto,
            Height_Agua,
            Temperatura,
            Volumen_agua,
            Version_Protocolo,
            Version_Device,


        }
        Comando_Medidor_fafnir Comando;

        byte[] TramaTx = new byte[1];
        byte[] TramaRx = new byte[1];

        bool SetDayTime;
        string FechaHora;


        bool EsTCPIP;
        string DireccionIP;
        string Puerto;


        int IdTurno, IdTurnoTemp;
        int CodigoTanque;
        bool ReportarInventario;
        bool ReportarInventarioTurno;
        bool EncuestaEnProceso;
        double Intervalo = 30;

        DateTime FechaInicial;
        DateTime FechaFinal;

        int suma = 0;//resultado de la comprovacion del Checksum

        byte[] Checksum_CS = new byte[3];



        Int16 Dato_Volumen, Dato_TCVolumen, Dato_Ullage, Dato_HeightProducto, Dato_HeightAgua, Dato_Temperatura, Dato_VolumenAgua = 0;
        bool analisa;
        string value = "";
        string CS = "0";
        Int16 CRC_TX = 0;


        private string FrecuenciaVeederEncuesta;

        #endregion

        #region PUNTO DE ARRANQUE DE LA CLASE


        public FAFNIR(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<int, Tanque> Tanques, ref SharedEventsFuelStation.CMensaje OEventosAutorizador, string Frecuencia)
        {
            try
            {

                if (!Directory.Exists(Application.StartupPath + "/LogueoFAFNIR"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoFAFNIR/");
                }
                PollingTimer = new System.Timers.Timer(30000);

                ArchivoTramas = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR -Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoRegistros = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR - Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                SWRegistros = File.AppendText(ArchivoRegistros);


                //SWRegistros.WriteLine(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2014-03-11 1000  Frecuencia:  " + Frecuencia);//Reintento de conexcion para el caso de Cruz roja
                //SWRegistros.Flush();

                SWRegistros.WriteLine(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
           " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2014-10-08 1000  Frecuencia:  " + Frecuencia);//Reintento de conexcion para el caso de Cruz roja
                SWRegistros.Flush();

                //Arma la Estructura de Tanques y sus propiedades
                TanquesVR = new Dictionary<int, Tanque>();
                TanquesVR = Tanques;

                foreach (Tanque Tanque in TanquesVR.Values)
                {

                    if (TanquesVR[Tanque.TankNumber].FactorVolumen == 0)
                        TanquesVR[Tanque.TankNumber].FactorVolumen = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorTCVolumen == 0)
                        TanquesVR[Tanque.TankNumber].FactorTCVolumen = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorUllage == 0)
                        TanquesVR[Tanque.TankNumber].FactorUllage = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorHeight == 0)
                        TanquesVR[Tanque.TankNumber].FactorHeight = 100;

                    if (TanquesVR[Tanque.TankNumber].FactorHeightAgua == 0)
                        TanquesVR[Tanque.TankNumber].FactorHeightAgua = 100;

                    if (TanquesVR[Tanque.TankNumber].FactorTemperatura == 0)
                        TanquesVR[Tanque.TankNumber].FactorTemperatura = 1000;

                    if (TanquesVR[Tanque.TankNumber].FactorVolumenAgua == 0)
                        TanquesVR[Tanque.TankNumber].FactorVolumenAgua = 10;



                    SWRegistros.WriteLine(DateTime.Now + "|Tanque|" + Tanque.TankNumber + "|- Datos: Factores Volumen = "
                        + TanquesVR[Tanque.TankNumber].FactorVolumen + " -Factores TCVolumen = "
                        + TanquesVR[Tanque.TankNumber].FactorTCVolumen + " -Factores Ullage = "
                        + TanquesVR[Tanque.TankNumber].FactorUllage + " -Factor Height = "
                        + TanquesVR[Tanque.TankNumber].FactorHeight + " -Factor HeightAgua = "
                        + TanquesVR[Tanque.TankNumber].FactorHeightAgua + " -FactorTemperatura = "
                        + TanquesVR[Tanque.TankNumber].FactorTemperatura + " -FactorVolumenAgua = "
                        + TanquesVR[Tanque.TankNumber].FactorVolumenAgua);




                    //SWRegistros.Write("Tanque: " + Tanque.TankNumber + " - ");
                    //SWRegistros.WriteLine();
                    SWRegistros.Flush();

                }


                SWRegistros.Write(DateTime.Now + "|Inicio|Configuracion|");
                foreach (Tanque Tanque in TanquesVR.Values)
                    SWRegistros.Write("Tanque: " + Tanque.TankNumber + " - ");
                SWRegistros.WriteLine();
                SWRegistros.Flush();

                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;

                if (EsTCPIP)
                {
                    //Crea y abre la conexión con el Servidor
                    ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                    Stream = ClienteVeederRoot.GetStream();
                }
                else
                {
                    PuertoFAFNIR.PortName = Puerto;
                    PuertoFAFNIR.BaudRate = 9600;
                    PuertoFAFNIR.DataBits = 8;
                    PuertoFAFNIR.StopBits = StopBits.One;
                    PuertoFAFNIR.Parity = Parity.None;
                    PuertoFAFNIR.ReadBufferSize = 4096;
                    PuertoFAFNIR.WriteBufferSize = 4096;

                    //Abre el puerto COM de comunicación con Veeder Root
                    PuertoFAFNIR.Open();
                }

                //Instancia los eventos de los objetos Timer
                if (Convert.ToInt64(Frecuencia) <= 0)
                {
                    Frecuencia = "1";
                }

                FrecuenciaVeederEncuesta = Frecuencia;
                FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(Frecuencia));
                FechaInicial = DateTime.Now;
                Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);

                PollingTimer.Dispose();
                PollingTimer = null;
                PollingTimer = new System.Timers.Timer(Intervalo);
                PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);


                //Instancia los eventos del SharedEvents
                oEventos = OEventosAutorizador;
                oEventos.InformarStocksTanques += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesEventHandler(oEventos_InformarStocksTanques);
                oEventos.InformarStocksTanquesCierreTurno += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesCierreTurnoEventHandler(oEventos_InformarStocksTanquesCierreTurno);
                oEventos.InformarEstadoVeederRoot += new SharedEventsFuelStation.__CMensaje_InformarEstadoVeederRootEventHandler(ExisteComunicacionVeederRootReciboCombustible);
                oEventos.ObtenerSaldoTanqueAjusteTurno += new SharedEventsFuelStation.__CMensaje_ObtenerSaldoTanqueAjusteTurnoEventHandler(oEventos_ObtenerSaldoTanqueAjusteTurno);
                oEventos.InformarStocksTanquesCierreTurnoServicio += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesCierreTurnoServicioEventHandler(oEventos_InformarStocksTanquesCierreTurnoServicio);

                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                PollingTimer.AutoReset = true;

                //Se activa el timer por primera vez
                PollingTimer.Start();


            }
            catch (Exception Excepcion)
            {

                string MensajeExcepcion = "Excepcion en el metodo VeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }


        void LimpiarVariableSocket()
        {
            try
            {
                ClienteVeederRoot.Close();
                Stream.Close();
                Stream.Dispose();
            }
            catch (Exception ex)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Metodo|" + "LimpiarVariableSocket: " + "Mensaje: " + ex.Message);
                SWRegistros.Flush();
            }

        }

        void ConfigurarTimer()
        {
            double TiemprElapse = 0;
            try
            {
                SWRegistros.WriteLine(DateTime.Now + "|Entro a la Configuracion  del Orquestador|Intervalo: " + Intervalo.ToString());
                SWRegistros.Flush();
                if (DateTime.Now > FechaFinal)
                {
                    FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(FrecuenciaVeederEncuesta));
                    FechaInicial = DateTime.Now;
                    Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                    //TiemprElapse = Intervalo;

                    if (Intervalo <= 0)
                    {
                        Intervalo = 160000;
                    }
                }
                else if (DateTime.Now == FechaFinal)
                {
                    FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(FrecuenciaVeederEncuesta));
                    FechaInicial = DateTime.Now;
                    Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                    TiemprElapse = Intervalo;

                    if (Intervalo <= 0)
                    {
                        FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(FrecuenciaVeederEncuesta));
                        FechaInicial = DateTime.Now;
                        Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                        //TiemprElapse = Intervalo;
                    }
                }
                else
                {
                    FechaInicial = DateTime.Now;
                    Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                    if (Intervalo <= 0)
                    {
                        FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(FrecuenciaVeederEncuesta));
                        FechaInicial = DateTime.Now;
                        Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                        //TiemprElapse = Intervalo;
                    }
                }


                PollingTimer.Dispose();
                PollingTimer = null;
                PollingTimer = new System.Timers.Timer(Convert.ToDouble(Intervalo));
                PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);
                PollingTimer.AutoReset = true;
                PollingTimer.Start();

                SWRegistros.WriteLine(DateTime.Now + "|Salio de la Configuracion  del Orquestador|Intervalo: " + Intervalo.ToString());
                SWRegistros.Flush();
                SWRegistros.WriteLine(DateTime.Now + "|Configuracion Orquestador|Intervalo: " + Intervalo.ToString());
                SWRegistros.Flush();
            }
            catch (Exception Excepcion)
            {

                string MensajeExcepcion = "Excepcion en el metodo ConfigurarTimer: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }

        }


        public FAFNIR(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<int, Tanque> Tanques)
        {
            try
            {

                PollingTimer = new System.Timers.Timer(100);//30000
                if (!Directory.Exists(Environment.CurrentDirectory + "/LogueoFAFNIR"))
                {
                    Directory.CreateDirectory(Environment.CurrentDirectory + "/LogueoFAFNIR/");
                }

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoTramas = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR -Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoRegistros = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR - Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                SWRegistros = File.AppendText(ArchivoRegistros);

                //Escribe el encabezado del archivo plano
                //SWRegistros.WriteLine(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //    " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 19/03/2014 - 1600");

                SWRegistros.WriteLine(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                    " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 20/06/2014 - 0847"); /// se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita

                SWRegistros.Flush();

                //Arma la Estructura de Tanques y sus propiedades
                TanquesVR = new Dictionary<int, Tanque>();
                TanquesVR = Tanques;

                //SWRegistros.Write(DateTime.Now + "|Inicio|Configuracion|");
                foreach (Tanque Tanque in TanquesVR.Values)
                {

                    if (TanquesVR[Tanque.TankNumber].FactorVolumen == 0)
                        TanquesVR[Tanque.TankNumber].FactorVolumen = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorTCVolumen == 0)
                        TanquesVR[Tanque.TankNumber].FactorTCVolumen = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorUllage == 0)
                        TanquesVR[Tanque.TankNumber].FactorUllage = 10;

                    if (TanquesVR[Tanque.TankNumber].FactorHeight == 0)
                        TanquesVR[Tanque.TankNumber].FactorHeight = 100;

                    if (TanquesVR[Tanque.TankNumber].FactorHeightAgua == 0)
                        TanquesVR[Tanque.TankNumber].FactorHeightAgua = 100;

                    if (TanquesVR[Tanque.TankNumber].FactorTemperatura == 0)
                        TanquesVR[Tanque.TankNumber].FactorTemperatura = 1000;

                    if (TanquesVR[Tanque.TankNumber].FactorVolumenAgua == 0)
                        TanquesVR[Tanque.TankNumber].FactorVolumenAgua = 10;



                    SWRegistros.WriteLine(DateTime.Now + "|Tanque|" + Tanque.TankNumber + "|- Datos: Factores Volumen = "
                        + TanquesVR[Tanque.TankNumber].FactorVolumen + " -Factores TCVolumen = "
                        + TanquesVR[Tanque.TankNumber].FactorTCVolumen + " -Factores Ullage = "
                        + TanquesVR[Tanque.TankNumber].FactorUllage + " -Factor Height = "
                        + TanquesVR[Tanque.TankNumber].FactorHeight + " -Factor HeightAgua = "
                        + TanquesVR[Tanque.TankNumber].FactorHeightAgua + " -FactorTemperatura = "
                        + TanquesVR[Tanque.TankNumber].FactorTemperatura + " -FactorVolumenAgua = "
                        + TanquesVR[Tanque.TankNumber].FactorVolumenAgua);




                    //SWRegistros.Write("Tanque: " + Tanque.TankNumber + " - ");
                    //SWRegistros.WriteLine();
                    SWRegistros.Flush();

                }

                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP;
                this.DireccionIP = DireccionIP;
                this.Puerto = Puerto;

                if (EsTCPIP)
                {
                    //Crea y abre la conexión con el Servidor
                    ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                    Stream = ClienteVeederRoot.GetStream();
                }
                else
                {
                    PuertoFAFNIR.PortName = Puerto;
                    PuertoFAFNIR.BaudRate = 9600;
                    PuertoFAFNIR.DataBits = 8;
                    PuertoFAFNIR.StopBits = StopBits.One;
                    PuertoFAFNIR.Parity = Parity.None;
                    PuertoFAFNIR.ReadBufferSize = 4096;
                    PuertoFAFNIR.WriteBufferSize = 4096;

                    //Abre el puerto COM de comunicación con Veeder Root
                    PuertoFAFNIR.Open();
                }

                //Instancia los eventos de los objetos Timer
                PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);

                //Instancia los eventos del SharedEvents
                Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                oEventos = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                oEventos.InformarStocksTanques += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesEventHandler(oEventos_InformarStocksTanques);
                oEventos.InformarStocksTanquesCierreTurno += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesCierreTurnoEventHandler(oEventos_InformarStocksTanquesCierreTurno);
                oEventos.InformarEstadoVeederRoot += new SharedEventsFuelStation.__CMensaje_InformarEstadoVeederRootEventHandler(ExisteComunicacionVeederRootReciboCombustible);
                //oEventos.ObtenerSaldoTanqueAjusteTurno += new SharedEventsFuelStation.__CMensaje_ObtenerSaldoTanqueAjusteTurnoEventHandler(oEventos_ObtenerSaldoTanqueAjusteTurno);//ojo descomentar error en el SharedEvent
                //oEventos.InformarStocksTanquesCierreTurnoServicio += new SharedEventsFuelStation.__CMensaje_InformarStocksTanquesCierreTurnoServicioEventHandler(oEventos_InformarStocksTanquesCierreTurnoServicio); //ojo descomentar error en el SharedEvent

                //oEventos.informars
                //Se configura el timer para el evento Elapsed se ejecute cada periodo de tiempo
                PollingTimer.AutoReset = true;

                //Se activa el timer por primera vez
                PollingTimer.Start();
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo VeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }




        #endregion


        //SE EJECUTA CADA PERIODO DE TIEMPO
        private void PollingTimerEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                //Setea variable que indica que ha comenzado la encuesta
                EncuestaEnProceso = true;
                SWRegistros.WriteLine(DateTime.Now + "|-----Entro a encuenta---");
                SWRegistros.Flush();

                //Se detiene el timer para realizar el respectivo proceso de encuesta
                PollingTimer.Stop();

                //Set_Time_Day: DCF 06-10-2012 1218
                if (!SetDayTime)
                {
                    //Consultar la version de Equipo y Protocolo

                    if (ProcesoEnvioComando(Comando_Medidor_fafnir.set_Day))
                    {
                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Se envía la Fecha al FAFNIR");
                        SWRegistros.Flush();
                    }

                    if (ProcesoEnvioComando(Comando_Medidor_fafnir.set_Time))
                    {

                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Se envía la Hora al FAFNIR");
                        SWRegistros.Flush();
                    }

                    if (ProcesoEnvioComando(Comando_Medidor_fafnir.Version_Protocolo)) ;
                    {
                        Asignar_Datos();

                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + " = " + value);
                        SWRegistros.Flush();
                    }


                    ProcesoEnvioComando(Comando_Medidor_fafnir.Version_Device);
                    {
                        Asignar_Datos();

                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + " = " + value);
                        SWRegistros.Flush();
                    }


                    SetDayTime = true; //Desactiva el set de Fecha-Hora y consulta de versiones.

                }


                //Ver
                VerifySizeFile();

                //Se intenta volver a conectar a la veeder-root
                if (this.EsTCPIP)
                {
                    VerificarConexion();
                }
                else
                {
                    VerificarConexionRS232();
                }

                //Envia comando y recibe respuesta de las Variables
                ProcesoReportarVariablesdeMedicionPeriodicamente();

                //Luego de realizado el proceso se reactiva el Timer
                //PollingTimer.Start();
                ConfigurarTimer();

                //Indica que ya finalizó el proceso
                EncuestaEnProceso = false;

                SWRegistros.WriteLine(DateTime.Now + "|Salio de la encuenta");
                SWRegistros.Flush();

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento PollingTimerEvent: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                EncuestaEnProceso = false;
            }
        }

        private void ProcesoReportarVariablesdeMedicionPeriodicamente()
        {
            try
            {

                {

                    AlmacenarInventario();

                    //Declara la coleccion
                    SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                    SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|INCIA PROCESO DE REPORTE DE VARIABLES DE MEDICION:");
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|**********************************************************************************************************************************************************");

                    string CodTanque;
                    string Stock;
                    string VolumenAgua;
                    short IdTipo;
                    double Valor;
                    //Arma la coleccion con datos 
                    foreach (Tanque oTanque in TanquesVR.Values)
                    {
                        SharedEventsFuelStation.ColAlarmas ListaAlarmas = new SharedEventsFuelStation.ColAlarmas();
                        SharedEventsFuelStation.ColVariables ListaVariables = new SharedEventsFuelStation.ColVariables();

                        CodTanque = oTanque.TankNumber.ToString();
                        Stock = oTanque.Volume.ToString("N3");
                        VolumenAgua = oTanque.WaterVolume.ToString("N3");
                        RedTanquesInventario.Add(ref CodTanque, ref IsTankActive, ref Stock, ref VolumenAgua);

                        //1. Stock
                        IdTipo = 1;
                        Valor = oTanque.Volume;
                        ListaVariables.Add(ref IdTipo, ref Valor);

                        //2. Stock Compensado
                        IdTipo = 2;
                        Valor = oTanque.TCVolume;
                        ListaVariables.Add(ref IdTipo, ref Valor);

                        //3. Temperatura
                        IdTipo = 3;
                        Valor = oTanque.Temperature;
                        ListaVariables.Add(ref IdTipo, ref Valor);

                        //4. Nivel de Agua
                        IdTipo = 4;
                        Valor = oTanque.Water;
                        ListaVariables.Add(ref IdTipo, ref Valor);

                        //TODO: Aquí van las alarmas

                        //Arma lista de Tanque con todas las variables: Inventario y Alarmas
                        RedTanquesVariables.AddMedicion(ref CodTanque, ref IsTankActive, ref ListaVariables, ref ListaAlarmas);

                        SWRegistros.WriteLine(
                                    "   Tank: " + oTanque.TankNumber +
                                    " - Product: " + oTanque.ProductCode +
                                    " - Volume: " + oTanque.Volume.ToString("N2") +
                                    " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                                    " - Ullage: " + oTanque.Ullage.ToString("N2") +
                                    " - Heigh: " + oTanque.Heigh.ToString("N2") +
                                    " - Water: " + oTanque.Water.ToString("N2") +
                                    " - Temperature: " + oTanque.Temperature.ToString("N2") +
                                    " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                    }



                    oEventos.SolicitarReportarAlarmasVariablesTanques(ref RedTanquesVariables);
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|FINALIZA PROCESO DE REPORTE DE VARIABLES DE MEDICION:");
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|**********************************************************************************************************************************************************");
                    SWRegistros.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }


        #region CONVERSIÓN Y ALMACENAMIENTO DE VARIABLES

        //ALMACENA EN EL DICCIONARIO DE TANQUES LOS VALORES OBTENIDO EN LA TRAMA
        private void AlmacenarInventario()
        {
            try
            {
                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|AlmacenarInventario|Inicia proceso de Almacenar Inventario:");
                SWRegistros.Flush();

                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Volume_Producto))//1°
                {
                    Asignar_Datos();
                }



                if (ProcesoEnvioComando(Comando_Medidor_fafnir.TC_volumen))//2°
                {
                    Asignar_Datos();
                }




                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Ullage))//3°
                {
                    Asignar_Datos();
                }



                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Height_Producto)) //4°
                {
                    Asignar_Datos();
                }


                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Height_Agua)) //5°
                {
                    Asignar_Datos();
                }



                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Temperatura)) //6°
                {
                    Asignar_Datos();
                }



                if (ProcesoEnvioComando(Comando_Medidor_fafnir.Volumen_agua))//7°
                {
                    Asignar_Datos();
                }

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|AlmacenarInventario|Finaliza proceso");
                SWRegistros.Flush();

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo AlmacenarInventario: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                ExisteValorInventario = true;
            }
        }

        private void Asignar_Datos()
        {
            Dato_Volumen = Dato_TCVolumen = Dato_Ullage = Dato_HeightProducto =
            Dato_HeightAgua = Dato_Temperatura = Dato_VolumenAgua = 0;

            analisa = false;

            value = "";//Limpiamos para los proximos datos.
            SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inicia proceso de Asignar_Datos TramaRX.lengt" + TramaRx.Length);
            SWRegistros.Flush();

            for (int i = 1; i < TramaRx.Length; i++)
            {
                if (TramaRx[i - 1] == '$')//activa la toma de datos = 0X24 '$'
                {
                    analisa = true;

                    if (Comando != Comando_Medidor_fafnir.Version_Device && Comando != Comando_Medidor_fafnir.Version_Protocolo)
                    {
                        string CodigoTanquexx = Convert.ToString(Convert.ToChar(TramaRx[i - 3])) + Convert.ToString(Convert.ToChar(TramaRx[i - 2]));

                        //CodigoTanque = Convert.ToInt16(CodigoTanquexx) - 1; //Asignamos el código del tanque a evaluar 
                        CodigoTanque = Convert.ToInt16(CodigoTanquexx); // 


                    }
                }

                if (TramaRx[i] == '=' || TramaRx[i] == ':' || (TramaRx[i - 1] == 0x0D && TramaRx[i] == 0x0A))// =0x3D
                {

                    if (analisa) // se almacena los datos en TanquesVR[CodigoTanque].XXXXXX
                    {


                        switch (Comando)
                        {

                            case Comando_Medidor_fafnir.Volume_Producto:
                                TanquesVR[CodigoTanque].Volume = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorVolumen;//una sifra decimal segun manual !!!

                                break;


                            case Comando_Medidor_fafnir.TC_volumen:
                                TanquesVR[CodigoTanque].TCVolume = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorTCVolumen;//una sifra decimal segun manual !!!
                                break;

                            case Comando_Medidor_fafnir.Ullage:
                                TanquesVR[CodigoTanque].Ullage = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorUllage;//una sifra decimal segun manual !!!
                                break;

                            case Comando_Medidor_fafnir.Height_Producto:
                                TanquesVR[CodigoTanque].Heigh = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorHeight;
                                break;

                            case Comando_Medidor_fafnir.Height_Agua:
                                TanquesVR[CodigoTanque].Water = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorHeightAgua;
                                break;

                            case Comando_Medidor_fafnir.Temperatura:
                                TanquesVR[CodigoTanque].Temperature = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorTemperatura;
                                break;

                            case Comando_Medidor_fafnir.Volumen_agua:
                                TanquesVR[CodigoTanque].WaterVolume = Convert.ToDouble(value) / TanquesVR[CodigoTanque].FactorVolumenAgua;
                                break;
                        }


                        value = "";
                    }

                    analisa = false;//Desactiva la Toma de datos 
                }


                //Char ccc = Convert.ToChar(TramaRx[i]);//solo inspeción 


                if (TramaRx[i] == ':')//se termino la entrega de datos, se recorrio toda la trama.
                {
                    break;
                }

                if (analisa == true)//Se almacena los datos de la medeción en value:
                {
                    value += Convert.ToChar(TramaRx[i]);
                }

            }



            //if (TanquesVR.ContainsKey(CodigoTanque))
            //{
            //    TanquesVR[CodigoTanque].TankNumber = CodigoTanque;
            //    TanquesVR[CodigoTanque].ProductCode = Convert.ToInt16(System.Text.Encoding.ASCII.GetString(TramaRx, j + 2, 1), 16);

            //    int NumeroVariables = Convert.ToInt16(System.Text.Encoding.ASCII.GetString(TramaRx, j + 7, 2), 16);
            //    if (NumeroVariables == 7)
            //    {
            //        TanquesVR[CodigoTanque].Volume = ObtenerValor(j + 9, 8);
            //        TanquesVR[CodigoTanque].TCVolume = ObtenerValor(j + 17, 8);
            //        TanquesVR[CodigoTanque].Ullage = ObtenerValor(j + 25, 8);
            //        TanquesVR[CodigoTanque].Heigh = ObtenerValor(j + 33, 8);
            //        TanquesVR[CodigoTanque].Water = ObtenerValor(j + 41, 8);
            //        TanquesVR[CodigoTanque].Temperature = ObtenerValor(j + 49, 8);
            //        TanquesVR[CodigoTanque].WaterVolume = ObtenerValor(j + 57, 8);
            //        ExisteValorInventario = true;

            //    }
            //}

        }


        //ASCII A PUNTO FLOTANTE
        private double ObtenerValor(int ElementoInicial, int Longitud)
        {
            try
            {
                //Obtiene el Signo
                //string Temporal = TramaRx[ElementoInicial].ToString("X2");
                int Signo = Convert.ToInt16(TramaRx[ElementoInicial].ToString("X2")) & 0x08; //AND 1000 0000
                if (Signo == 0x08)
                    Signo = -1;
                else
                    Signo = 1;

                //Calcula el Exponente   
                long lExponente = Convert.ToInt64(System.Text.Encoding.ASCII.GetString(TramaRx, ElementoInicial, 4), 16) & 0x7F80; //AND 0111 1111 1000 0000
                lExponente = lExponente >> 7;
                double Exponente = System.Math.Pow(2, lExponente - 127);

                //Obtiene la Mantisa
                double Mantisa = Convert.ToInt64(System.Text.Encoding.ASCII.GetString(TramaRx, ElementoInicial + 2, 6), 16) & 0x7FFFFF;
                Mantisa = 1 + Mantisa / System.Math.Pow(2, 23);

                //Calcula el valor resultado a partir de la mantisa, el signo y el exponente
                return Signo * Exponente * Mantisa;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ObtenerValor: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                return 0;
            }
        }

        #endregion

        #region COMUNICACION CON VEEDER ROOT: ENVIO, RECEPCION Y VALIDACIÓN DE INTEGRIDAD DE TRAMAS

        //INICIA EL PROCESO DE ENVIO DE COMANDO Y RECEPCION DE INFORMACION DE LA VEEDER ROOT
        private bool ProcesoEnvioComando(Comando_Medidor_fafnir Comando)
        {
            try
            {
                this.Comando = Comando;
                ArmarTramaTx();

                bool ComandoExitoso = true;

                while (EnvioComando)
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Proceso Envio Comando: " + EnvioComando.ToString() + " Espera obligatoria");
                    SWRegistros.Flush();
                    Thread.Sleep(1000);
                }
                if (EsTCPIP)
                {
                    if (!EnviarTramaTCPIP())
                        ComandoExitoso = false;
                }
                else
                {
                    if (!EnviarTramaRS232())
                        ComandoExitoso = false;
                }

                if (ComandoExitoso && ValidarIntegridadTramaRx())
                    return true;
                else
                    return false;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ProcesoEnvioComando: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                return false;
            }
        }

        //CONSTRUYE LA TRAMA DE ENVIO A LA VEEDER ROOT
        private void ArmarTramaTx()
        {
            try
            {
                switch (Comando)
                {
                    case Comando_Medidor_fafnir.set_Day://OK
                        #region "ComandoVeederRoot.set_Day"
                        string YYMMDD = (DateTime.Now.Year.ToString().Remove(0, 2) + DateTime.Now.Month.ToString().PadLeft(2, '0') + DateTime.Now.Day.ToString().PadLeft(2, '0'));

                        byte año0 = Convert.ToByte(YYMMDD.Substring(0, 1));
                        byte año1 = Convert.ToByte(YYMMDD.Substring(1, 1));
                        byte mes0 = Convert.ToByte(YYMMDD.Substring(2, 1));
                        byte mes1 = Convert.ToByte(YYMMDD.Substring(3, 1));
                        byte dia0 = Convert.ToByte(YYMMDD.Substring(4, 1));
                        byte dia1 = Convert.ToByte(YYMMDD.Substring(5, 1));

                        //QDW$161203:121<CR><LF>  Q    D     W     $            Y                    Y                    M                    M                    D                     D            :      cs    cs    cs    cr   lf
                        TramaTx = new byte[11] { 0x51, 0x44, 0x57, 0X24, (byte)(año0 + 0x30), (byte)(año1 + 0x30), (byte)(mes0 + 0x30), (byte)(mes1 + 0x30), (byte)(dia0 + 0x30), (byte)(dia1 + 0x30), 0x3A };

                        Checksum_TX();

                        TramaTx = new byte[16] { 0x51, 0x44, 0x57, 0X24, (byte)(año0 + 0x30), (byte)(año1 + 0x30), (byte)(mes0 + 0x30), 
                                                (byte)(mes1 + 0x30), (byte)(dia0 + 0x30), (byte)(dia1 + 0x30), 0x3A,(byte)(Checksum_CS[0] + 0x30),
                                                (byte)(Checksum_CS[1]+ 0x30),(byte)(Checksum_CS[2] + 0x30), 0x0D, 0x0A };

                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Fecha a enviar al FAFNIR = " + DateTime.Now.Year.ToString().Remove(0, 2) + "/" +
                                              DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Day.ToString().PadLeft(2, '0')); // Borrar solo para inspección 
                        SWRegistros.Flush();
                        break;
                        #endregion

                    case Comando_Medidor_fafnir.set_Time://OK
                        #region "ComandoVeederRoot.set_Time"
                        string HHMMSS = (DateTime.Now.Hour.ToString().PadLeft(2, '0') + DateTime.Now.Minute.ToString().PadLeft(2, '0') + DateTime.Now.Second.ToString().PadLeft(2, '0'));

                        byte H0 = Convert.ToByte(HHMMSS.Substring(0, 1));
                        byte H1 = Convert.ToByte(HHMMSS.Substring(1, 1));
                        byte M0 = Convert.ToByte(HHMMSS.Substring(2, 1));
                        byte M1 = Convert.ToByte(HHMMSS.Substring(3, 1));
                        byte S0 = Convert.ToByte(HHMMSS.Substring(4, 1));
                        byte S1 = Convert.ToByte(HHMMSS.Substring(5, 1));

                        //QCW$161203:121<CR><LF>  Q      C     W     $             H                      H                  M              M                  S                 S             :      cs    cs    cs    cr   lf
                        TramaTx = new byte[11] { 0x51, 0x43, 0x57, 0X24, (byte)(H0 + 0x30), (byte)(H1 + 0x30), (byte)(M0 + 0x30), (byte)(M1 + 0x30), (byte)(S0 + 0x30), (byte)(S1 + 0x30), 0x3A };

                        Checksum_TX();

                        TramaTx = new byte[16] { 0x51, 0x43, 0x57, 0X24, (byte)(H0 + 0x30), (byte)(H1 + 0x30), (byte)(M0 + 0x30), 
                                                (byte)(M1 + 0x30), (byte)(S0 + 0x30), (byte)(S1 + 0x30), 0x3A,(byte)(Checksum_CS[0] + 0x30),
                                                (byte)(Checksum_CS[1]+ 0x30),(byte)(Checksum_CS[2] + 0x30), 0x0D, 0x0A };

                        SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Fecha a enviar al FAFNIR = " + DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Second.ToString().PadLeft(2, '0')); // Borrar solo para inspección 
                        SWRegistros.Flush();
                        break;


                        #endregion


                    case Comando_Medidor_fafnir.Volume_Producto://current volume compensated to reference temperature
                        //QV3 0 <CR><LF> //51 56 33 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x56, 0x33, 0x20, 0x30, 0x0D, 0x0A };
                        break;

                    case Comando_Medidor_fafnir.TC_volumen:
                        //QV5 0 <CR><LF> //51 56 35 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x56, 0x35, 0x20, 0x30, 0x0D, 0x0A };
                        break;

                    case Comando_Medidor_fafnir.Ullage:
                        //QV4 0 <CR><LF> //51 56 34 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x56, 0x34, 0x20, 0x30, 0x0D, 0x0A };

                        break;

                    case Comando_Medidor_fafnir.Height_Producto:
                        //QLP 0 <CR><LF> //51 4C 50 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x4C, 0x50, 0x20, 0x30, 0x0D, 0x0A };
                        break;

                    case Comando_Medidor_fafnir.Height_Agua:
                        //QLW 0 <CR><LF> //51 4C 57 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x4C, 0x57, 0x20, 0x30, 0x0D, 0x0A };
                        break;

                    case Comando_Medidor_fafnir.Temperatura:
                        //HTP 0<CR><LF>
                        TramaTx = new byte[7] { 0X48, 0X54, 0X50, 0X20, 0X30, 0X0D, 0X0A };
                        break;

                    case Comando_Medidor_fafnir.Volumen_agua:
                        //QVW 0 <CR><LF> //51 56 57 20 30 0D 0A --> 0 para todo los tanques                      
                        TramaTx = new byte[7] { 0x51, 0x56, 0x57, 0x20, 0x30, 0x0D, 0x0A };

                        break;

                    case Comando_Medidor_fafnir.Version_Protocolo:
                        // QVE<CR><LF> //51 56 45 0D 0A 
                        TramaTx = new byte[5] { 0x51, 0x56, 0x45, 0x0D, 0x0A };
                        break;


                    case Comando_Medidor_fafnir.Version_Device:
                        //QVD<CR><LF> //  51 56 44 0D 0A
                        TramaTx = new byte[5] { 0x51, 0x56, 0x44, 0x0D, 0x0A };

                        break;


                    //????
                    //case Comando_Medidor_fafnir.Inventario:
                    //    //{SOH i 2 0 1 T T } TT = 00 Todos los tanques
                    //    TramaTx = System.Text.Encoding.ASCII.GetBytes(Convert.ToChar(0x01) + "i20100");// + CodigoTanque.ToString().PadLeft(2, '0'));
                    //    break;

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ArmarTramaTx: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        //CONFIGURACION DE LOS PARAMETROS RESPECTIVOS Y ENVÍA PARA COMUNICAR POR UN SOCKET TCP/IP
        private bool EnviarTramaTCPIP()
        {
            byte[] TramaRxTemporal = new byte[1000];
            int BytesRecibidos = 0;
            EnvioComando = true;
            int NumReintentos = 0;
            try
            {
                try
                {
                    VerificarConexion();//Verifico la conexcion antes de realizar un consulta
                    Stream.Write(TramaTx, 0, TramaTx.Length);
                    Stream.Flush();

                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        SWRegistros.WriteLine(DateTime.Now + "|Reintento de envio de comando");
                        SWTramas.Flush();
                        Thread.Sleep(2000);
                        VerificarConexion();
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                    }
                    catch (Exception)
                    {

                    }
                }
                catch (System.Net.Sockets.SocketException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        SWRegistros.WriteLine(DateTime.Now + "|Reintento de envio de comando");
                        SWTramas.Flush();
                        Thread.Sleep(2000);
                        VerificarConexion();
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                    }
                    catch (Exception)
                    {

                    }

                }
                catch (Exception)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        SWRegistros.WriteLine(DateTime.Now + "|Reintento de envio de comando");
                        SWTramas.Flush();
                        Thread.Sleep(2000);
                        VerificarConexion();
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                    }
                    catch (Exception)
                    {

                    }

                }



                //Loguea en el archivo plano la trama enviada
                SWTramas.WriteLine(DateTime.Now + "|Tx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaTx));
                SWTramas.Flush();
                Stream.ReadTimeout = 6000;
                SWRegistros.WriteLine(DateTime.Now + "|Antes de leer TCP-IP|");
                SWTramas.Flush();
                Thread.Sleep(3000);

                //Reintentos en caso de falla en la comunicacion 2013-03-29
                try
                {
                    if (Stream.CanRead)
                    {
                        do
                        {
                            //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                            BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                        } while (Stream.DataAvailable);
                    }

                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        TramaRxTemporal = new byte[1000];
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;
                        Thread.Sleep(3000);
                        if (Stream.CanRead)
                        {
                            do
                            {
                                //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                                BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                            } while (Stream.DataAvailable);
                        }

                    }
                    catch (Exception)
                    {

                        SWRegistros.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();
                        BytesRecibidos = 0;
                    }

                }
                catch (System.Net.Sockets.SocketException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        VerificarConexion();
                        TramaRxTemporal = new byte[1000];
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;
                        Thread.Sleep(3000);
                        if (Stream.CanRead)
                        {
                            do
                            {
                                //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                                BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                            } while (Stream.DataAvailable);
                        }

                    }
                    catch (Exception)
                    {

                        SWRegistros.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();
                        BytesRecibidos = 0;
                    }


                }
                catch (Exception)//Si genera error lo capturo, espero y reenvio el comando
                {

                    try
                    {
                        VerificarConexion();
                        TramaRxTemporal = new byte[1000];
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                        Stream.ReadTimeout = 6000;
                        Thread.Sleep(3000);
                        if (Stream.CanRead)
                        {
                            do
                            {
                                //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                                BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                            } while (Stream.DataAvailable);
                        }

                    }
                    catch (Exception)
                    {

                        SWRegistros.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                        SWTramas.Flush();
                        BytesRecibidos = 0;
                    }

                }


                SWRegistros.WriteLine(DateTime.Now + "|Despues de leer TCP-IP -Bytes Recibidos de la Veeder Root:  " + BytesRecibidos.ToString());
                SWTramas.Flush();

                while (NumReintentos <= 20 && BytesRecibidos <= 0)//Reintento de reconexcion en caso que  la veeder responda con 0 Bytes
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Reintento de Peticion #: " + NumReintentos.ToString() + "|");
                    SWTramas.Flush();

                    if (BytesRecibidos <= 0)
                    {
                        try
                        {

                            VerificarConexion();
                            TramaRxTemporal = new byte[1000];
                            Stream.Write(TramaTx, 0, TramaTx.Length);
                            Stream.Flush();
                            Stream.ReadTimeout = 6000;
                            Thread.Sleep(3000);

                            if (Stream.CanRead)
                            {
                                do
                                {
                                    //Cambio en en el tiempo de espera de la lectura del buffer TCP //2013-03-27 0812
                                    BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

                                } while (Stream.DataAvailable);
                            }

                        }
                        catch (Exception ex2)
                        {

                            SWRegistros.WriteLine(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());
                            SWTramas.Flush();
                            BytesRecibidos = 0;
                            Thread.Sleep(3000);
                        }
                        NumReintentos++;
                    }
                    else
                    {
                        NumReintentos++;
                    }

                }


                if (BytesRecibidos > 0)
                {
                    //Almacena la información recibida en TramaRx    

                    TramaRx = new byte[BytesRecibidos];
                    for (int i = 0; i < TramaRx.Length; i++)
                        TramaRx[i] = TramaRxTemporal[i];

                    //Loguea en el archivo plano la trama recibiida
                    SWRegistros.WriteLine(DateTime.Now + "|TramaRx Cantidad Bytes Recibidos:  " + BytesRecibidos.ToString());
                    SWTramas.WriteLine(DateTime.Now + "|Rx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaRx));
                    SWTramas.Flush();
                    EnvioComando = false;
                    return true;
                }
                else
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Error|" + Comando + "|Veeder Root no envio informacion");
                    SWRegistros.Flush();
                    EnvioComando = false;
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo EstablecerComunicacionTCPIP: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                EnvioComando = false;
                return false;
            }
        }

        //CONFIGURACION DE LOS PARAMETROS PARA COMUNICAR POR UNA CONEXIÓN RS232
        private bool EnviarTramaRS232()
        {

            EnvioComando = true;
            try
            {
                //Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                PuertoFAFNIR.DiscardOutBuffer();
                PuertoFAFNIR.DiscardInBuffer();

                //Loguea en el archivo plano la trama TX
                SWTramas.WriteLine(DateTime.Now + "|Tx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaTx));
                SWTramas.Flush();

                //Escribe en el puerto el comando a Enviar.
                PuertoFAFNIR.Write(TramaTx, 0, TramaTx.Length);
                //Thread.Sleep(3000);
                Thread.Sleep(1300);

                int BytesRecibidos = PuertoFAFNIR.BytesToRead;


                if (BytesRecibidos > 0)
                {
                    TramaRx = new byte[BytesRecibidos];

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    PuertoFAFNIR.Read(TramaRx, 0, BytesRecibidos);


                    //Volumen 
                    //datos simiulados OJO borrar
                    //TramaRx = new byte[48]  { 0x51, 0x56, 0x33, 0x3D , 0x20 , 0x31 , 0x24 , 0x20 , 0x20 , 0x20 , 0x20 , 0x35 ,
                    //0x35 , 0x36, 0x36 , 0x35, 0x3D , 0x20, 0x32 , 0x24 , 0x20 , 0x20 , 0x20 , 0x20 , 0x31 , 0x30 , 
                    //0x32 , 0x34 , 0x32 , 0x3D , 0x20 , 0x33 , 0x24 , 0x20 , 0x20 , 0x20 , 0x20 , 0x37 , 0x33 , 0x39 , 0x30
                    //, 0x39 , 0x3A , 0x31 , 0x39 , 0x36 , 0x0D, 0X0A };


                    //VolumenTC
                    //TramaRx = new byte[48]  {0x51,0x56,0x35,0x3D,0x20,0x31,0x24,0x20,0x20 ,0x20 ,0x20 ,0x35 ,0x34 ,0x34 ,0x37 ,0x37
                    //    ,0x3D ,0x20 ,0x32 ,0x24 ,0x20 ,0x20 ,0x20 ,0x20 ,0x31 ,0x30 ,0x30 ,0x34 ,0x35 ,0x3D ,0x20 ,0x33 ,0x24 
                    //,0x20 ,0x20 ,0x20 ,0x20 ,0x37 ,0x32 ,0x37 ,0x31 ,0x31 ,0x3A ,0x31 ,0x38 ,0x39 ,0x0D ,0x0A};


                    ////Merma
                    //TramaRx = new byte[48]  {0x51,0x56,0x34,0x3D,0x20,0x31,0x24,0x20,0x20,0x20,0x20,0x32,0x35,0x38,0x36,0x32,0x3D,0x20,0x32,
                    //0x24,0x20,0x20,0x20,0x20,0x31,0x34,0x39,0x38,0x31,0x3D,0x20,0x33,0x24,0x20,0x20,0x20,0x20,0x33
                    //,0x35,0x37,0x30,0x30,0x3A,0x31,0x39,0x34,0x0D,0x0A};



                    //VOLUMEN AGUA.

                    //TramaRx = new byte[48]  { 0X51,0x56,0x57,0x3D,0x20,0x31,0x24,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x30,0x3D,0x20,0x32,0x24,
                    //                          0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x30,0x3D,0x20,0x33,0x24,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    //                          0x20,0x30,0x3A,0x32,0x33,0x31,0x0D,0x0A};


                    //Loguea en el archivo plano la trama recibiida
                    SWTramas.WriteLine(DateTime.Now + "|Rx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaRx));
                    SWTramas.Flush();
                    EnvioComando = false;

                    return true;
                }
                else
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Error|" + Comando + "|Veeder Root no envio informacion");
                    SWRegistros.Flush();
                    EnvioComando = false;
                    return false;
                }


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo EnviarTramaRS23: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                EnvioComando = false;
                return false;
            }
        }

        //VALIDA QUE LA TRAMA ENVIADA POR LA VEEDER ROOT SEA CONSISTENTE SEGÚN ESTRUCTURA
        private bool ValidarIntegridadTramaRx()
        {
            try
            {
                //Valida Encabezado de trama
                if (TramaRx[0] != TramaTx[0] || TramaRx[1] != TramaTx[1] || TramaRx[2] != TramaTx[2])
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Inconsistencia|" + Comando + "|Encabezado errado: " + TramaRx[0]);
                    SWRegistros.Flush();
                    return false;
                }
                else
                {
                    //Valida Fin de trama
                    if (TramaRx[TramaRx.Length - 2] != 0x0D || TramaRx[TramaRx.Length - 1] != 0x0A)
                    {
                        SWRegistros.WriteLine(DateTime.Now + "|Inconsistencia|" + Comando + "|Fin de trama errado: " + TramaRx[TramaRx.Length - 2] + " / " + TramaRx[TramaRx.Length - 1]);
                        SWRegistros.Flush();
                        return false;
                    }
                    else
                    {
                        CS = " ";
                        CS += Convert.ToChar(TramaRx[TramaRx.Length - 5]);
                        CS += Convert.ToChar(TramaRx[TramaRx.Length - 4]);
                        CS += Convert.ToChar(TramaRx[TramaRx.Length - 3]);

                        CRC_TX = Convert.ToInt16(CS);


                        Checksum_Calculado_RX();//Calcula el CS por medio de la suma y diferencia 255

                        if (CRC_TX != suma)
                        {
                            SWRegistros.WriteLine(DateTime.Now + "|Inconsistencia|" + Comando + "|Checksum Recibido: " + CRC_TX +
                                " - Checksum Calculado: " + suma);
                            SWRegistros.Flush();
                            return false;
                        }
                        else
                            return true;//CRC correcto 
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ValidarIntegridadTramaRx: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                return false;
            }
        }

        //CALCULA EL CHECKSUM QUE DEBE RECIBIR EN LA TRAMA DE RECEPCION
        private long Checksum_TX()
        {
            try
            {
                suma = 0x3A;
                for (int i = 0; (TramaTx[i] != 0x3A); i++)
                {
                    suma = suma + TramaTx[i];
                }


                for (int j = 0; suma > 255; j++)
                {
                    suma = suma - 255;
                }


                Checksum_CS[0] = Convert.ToByte(suma / 100 % 10);
                Checksum_CS[1] = Convert.ToByte(suma / 10 % 10);
                Checksum_CS[2] = Convert.ToByte(suma / 1 % 10);


                return (suma);

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo Checksum: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                return 0;
            }
        }

        #endregion

        private long Checksum_Calculado_RX()
        {
            try
            {
                suma = 0x3A;
                for (int i = 0; (TramaRx[i] != 0x3A); i++)
                {
                    suma = suma + TramaRx[i];
                }


                for (int j = 0; suma > 255; j++)
                {
                    suma = suma - 255;
                }


                //Checksum_CS[0] = Convert.ToByte(suma /100 % 10);
                //Checksum_CS[1] = Convert.ToByte(suma/10 % 10);
                //Checksum_CS[2] = Convert.ToByte(suma/1 % 10);


                return (suma);

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo Checksum: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
                return 0;
            }
        }

        #region COMUNICACION CON AUTORIZADOR (CORE)

        private bool ProcesoValidacionComunicacionVeederRoot()
        {
            bool ProcesoOk = false;
            try
            {
                //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inicia proceso de Validacion Comunicacion :");
                //ProcesoOk = ProcesoEnvioComando(Comando_Medidor_fafnir.Inventario);
                //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Envio comando a Consola: " + ProcesoOk.ToString());

                //if (ProcesoOk)
                //{


                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|ProcesoValidacionComunicacionVeederRoot()|Inicia proceso de Validacion Comunicacion VeederRoot:");
                SWRegistros.Flush();

                AlmacenarInventario();

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|ProcesoValidacionComunicacionVeederRoot()|Finaliza proceso de Validacion Comunicacion VeederRoot:");
                SWRegistros.Flush();


                //Declara la coleccion
                SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                bool IsTankActive = true;

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Proceso de Validacion: Inicia a registrar la informacion de los tanques");

                string CodTanque;
                string Stock;
                string VolumenAgua;
                short IdTipo;
                double Valor;
                //Arma la coleccion con datos 
                foreach (Tanque oTanque in TanquesVR.Values)
                {
                    SharedEventsFuelStation.ColAlarmas ListaAlarmas = new SharedEventsFuelStation.ColAlarmas();
                    SharedEventsFuelStation.ColVariables ListaVariables = new SharedEventsFuelStation.ColVariables();

                    CodTanque = oTanque.TankNumber.ToString();
                    Stock = oTanque.Volume.ToString("N3");
                    VolumenAgua = oTanque.Water.ToString("N3");
                    RedTanquesInventario.Add(ref CodTanque, ref IsTankActive, ref Stock, ref VolumenAgua);

                    //1. Stock
                    IdTipo = 1;
                    Valor = oTanque.Volume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //2. Stock Compensado
                    IdTipo = 2;
                    Valor = oTanque.TCVolume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //3. Temperatura
                    IdTipo = 3;
                    Valor = oTanque.Temperature;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //4. Nivel de Agua
                    IdTipo = 4;
                    Valor = oTanque.Water;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //TODO: Aquí van las alarmas

                    //Arma lista de Tanque con todas las variables: Inventario y Alarmas
                    RedTanquesVariables.AddMedicion(ref CodTanque, ref IsTankActive, ref ListaVariables, ref ListaAlarmas);


                    SWRegistros.WriteLine(
                                "   Tank: " + oTanque.TankNumber +
                                " - Product: " + oTanque.ProductCode +
                                " - Volume: " + oTanque.Volume.ToString("N2") +
                                " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                                " - Ullage: " + oTanque.Ullage.ToString("N2") +
                                " - Heigh: " + oTanque.Heigh.ToString("N2") +
                                " - Water: " + oTanque.Water.ToString("N2") +
                                " - Temperature: " + oTanque.Temperature.ToString("N2") +
                                " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                }


                SWRegistros.WriteLine(DateTime.Now + "|==========================================================================");
                SWRegistros.Flush();
                SWRegistros.WriteLine(DateTime.Now + "|==========================================================================");
                SWRegistros.Flush();
                ProcesoOk = true;
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en ProcesoValidacionComunicacionVeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
            return ProcesoOk;
        }

        //PROCESO PARA OBTENCION DE VARIABLES DE VEEDER ROOT    
        private void ProcesoObtencionReporteVariables()
        {
            try
            {
                //if (ProcesoEnvioComando(Comando_Medidor_fafnir.Inventario))
                //{

                AlmacenarInventario();

                //Declara la coleccion
                SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                bool IsTankActive = true;

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");

                string CodTanque;
                string Stock;
                string VolumenAgua;
                short IdTipo;
                double Valor;
                //Arma la coleccion con datos 
                foreach (Tanque oTanque in TanquesVR.Values)
                {
                    SharedEventsFuelStation.ColAlarmas ListaAlarmas = new SharedEventsFuelStation.ColAlarmas();
                    SharedEventsFuelStation.ColVariables ListaVariables = new SharedEventsFuelStation.ColVariables();

                    CodTanque = oTanque.TankNumber.ToString();
                    Stock = oTanque.Volume.ToString("N3");
                    VolumenAgua = oTanque.WaterVolume.ToString("N3");
                    RedTanquesInventario.Add(ref CodTanque, ref IsTankActive, ref Stock, ref VolumenAgua);

                    //1. Stock
                    IdTipo = 1;
                    Valor = oTanque.Volume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //2. Stock Compensado
                    IdTipo = 2;
                    Valor = oTanque.TCVolume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //3. Temperatura
                    IdTipo = 3;
                    Valor = oTanque.Temperature;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //4. Nivel de Agua
                    IdTipo = 4;
                    Valor = oTanque.Water;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //TODO: Aquí van las alarmas

                    //Arma lista de Tanque con todas las variables: Inventario y Alarmas
                    RedTanquesVariables.AddMedicion(ref CodTanque, ref IsTankActive, ref ListaVariables, ref ListaAlarmas);

                    SWRegistros.WriteLine(
                                "   Tank: " + oTanque.TankNumber +
                                " - Product: " + oTanque.ProductCode +
                                " - Volume: " + oTanque.Volume.ToString("N2") +
                                " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                                " - Ullage: " + oTanque.Ullage.ToString("N2") +
                                " - Heigh: " + oTanque.Heigh.ToString("N2") +
                                " - Water: " + oTanque.Water.ToString("N2") +
                                " - Temperature: " + oTanque.Temperature.ToString("N2") +
                                " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                }

                //Si se requiere enviar el inventario por petición de la Terminal
                if (ReportarInventario)
                {
                    oEventos.SolicitarEnviarInformacionTanques(ref RedTanquesInventario);
                    ReportarInventario = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición MR");
                    SWRegistros.Flush();
                }
                //Si se requiere enviar el inventario por Cierre de Turno
                else if (ReportarInventarioTurno)
                {
                    oEventos.SolicitarEnviarInformacionTanquesCierreTurno(ref RedTanquesInventario, ref IdTurno);
                    ReportarInventarioTurno = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición Cierre de Turno");
                    SWRegistros.Flush();
                }

                //oEventos.SolicitarReportarAlarmasVariablesTanques(ref RedTanquesVariables);
                //SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                //SWRegistros.Flush();
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        private void ProcesoObtencionReporteVariables(bool EsTurno)
        {
            try
            {
                //if (ProcesoEnvioComando(Comando_Medidor_fafnir.Inventario))
                //{

                AlmacenarInventario();

                //Declara la coleccion
                SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                bool IsTankActive = true;

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");

                string CodTanque;
                string Stock;
                string VolumenAgua;
                short IdTipo;
                double Valor;
                //Arma la coleccion con datos 
                foreach (Tanque oTanque in TanquesVR.Values)
                {
                    SharedEventsFuelStation.ColAlarmas ListaAlarmas = new SharedEventsFuelStation.ColAlarmas();
                    SharedEventsFuelStation.ColVariables ListaVariables = new SharedEventsFuelStation.ColVariables();

                    CodTanque = oTanque.TankNumber.ToString();
                    Stock = oTanque.Volume.ToString("N3");
                    VolumenAgua = oTanque.WaterVolume.ToString("N3");
                    RedTanquesInventario.Add(ref CodTanque, ref IsTankActive, ref Stock, ref VolumenAgua);

                    //1. Stock
                    IdTipo = 1;
                    Valor = oTanque.Volume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //2. Stock Compensado
                    IdTipo = 2;
                    Valor = oTanque.TCVolume;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //3. Temperatura
                    IdTipo = 3;
                    Valor = oTanque.Temperature;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //4. Nivel de Agua
                    IdTipo = 4;
                    Valor = oTanque.Water;
                    ListaVariables.Add(ref IdTipo, ref Valor);

                    //TODO: Aquí van las alarmas

                    //Arma lista de Tanque con todas las variables: Inventario y Alarmas
                    RedTanquesVariables.AddMedicion(ref CodTanque, ref IsTankActive, ref ListaVariables, ref ListaAlarmas);

                    SWRegistros.WriteLine(
                                "   Tank: " + oTanque.TankNumber +
                                " - Product: " + oTanque.ProductCode +
                                " - Volume: " + oTanque.Volume.ToString("N2") +
                                " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                                " - Ullage: " + oTanque.Ullage.ToString("N2") +
                                " - Heigh: " + oTanque.Heigh.ToString("N2") +
                                " - Water: " + oTanque.Water.ToString("N2") +
                                " - Temperature: " + oTanque.Temperature.ToString("N2") +
                                " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                }

                //Si se requiere enviar el inventario por petición de la Terminal
                if (EsTurno)
                {
                    oEventos.SolicitarEnviarInformacionTanquesCierreTurnoServicio(ref RedTanquesInventario, ref IdTurnoTemp);//ojo descomentar error en el SharedEvent
                    ReportarInventarioTurno = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición Cierre de Turno");
                    SWRegistros.Flush();
                }

                //oEventos.SolicitarReportarAlarmasVariablesTanques(ref RedTanquesVariables);
                //SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                //SWRegistros.Flush();
                //}
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }
        #endregion



        private void ObetenerStockValidacionCierreTurno(ref SharedEventsFuelStation.ColTanques Tanques)
        {
            try
            {
                //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para inicar proceso de stock:");
                //SWRegistros.Flush();
                if (EsTCPIP)
                {
                    //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para verificar concexcion IP:");
                    //SWRegistros.Flush();
                    VerificarConexion();
                }
                else
                {
                    //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para verificar concexcion rc232:");
                    //SWRegistros.Flush();
                    VerificarConexionRS232();
                }


                if (Tanques == null)
                {
                    Tanques = new SharedEventsFuelStation.ColTanques();
                    //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo para iniciar la colecion:");
                    //SWRegistros.Flush();
                }

                //if (ProcesoEnvioComando(Comando_Medidor_fafnir.Inventario))
                //{
                //    //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo para Tomar valores de la veeder");
                //SWRegistros.Flush();


                AlmacenarInventario();
                ExisteValorInventario = true;
                //SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Finalizo al metodo para Tomar valores de la veeder");
                //SWRegistros.Flush();

                //Declara la coleccion                   
                // SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                bool IsTankActive = true;

                SWRegistros.WriteLine(DateTime.Now + "|Proceso|" + Comando + "|Inventario Validacion Cierre Tanques:");

                string CodTanque;
                string Stock;
                string VolumenAgua;
                //short IdTipo;
                //double Valor;
                //Arma la coleccion con datos 
                foreach (Tanque oTanque in TanquesVR.Values)
                {

                    CodTanque = oTanque.TankNumber.ToString();
                    Stock = oTanque.Volume.ToString("N3");
                    VolumenAgua = oTanque.WaterVolume.ToString("N3");
                    Tanques.Add(ref CodTanque, ref IsTankActive, ref Stock, ref VolumenAgua);


                    SWRegistros.WriteLine("Saldo de Tanques para Validacion en Ajustes por Turno" +
                                "   Tank: " + oTanque.TankNumber +
                                " - Product: " + oTanque.ProductCode +
                                " - Volume: " + oTanque.Volume.ToString("N2") +
                                " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                                " - Ullage: " + oTanque.Ullage.ToString("N2") +
                                " - Heigh: " + oTanque.Heigh.ToString("N2") +
                                " - Water: " + oTanque.Water.ToString("N2") +
                                " - Temperature: " + oTanque.Temperature.ToString("N2") +
                                " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                }


                SWRegistros.Flush();
                //}

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObetenerStockValidacionCierreTurno: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        #region EVENTOS DE LA CLASE


        public void oEventos_ObtenerSaldoTanqueAjusteTurno(ref SharedEventsFuelStation.ColTanques Tanques)
        {
            int i = 0;
            try
            {

                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|oEventos_ObtenerSaldoTanqueAjusteTurno|Perdida de comunicacion - Intento de reconexion");
                SWRegistros.Flush();

                while ((i <= 20) || ExisteValorInventario != true)
                {
                    ObetenerStockValidacionCierreTurno(ref Tanques);
                    i++;
                    if (ExisteValorInventario == true)
                    {
                        break;
                    }

                }
                ExisteValorInventario = false;

                if (i == 10)
                {
                    string MensajeExcepcion = "No se pudo obetener el inventario de la Veeder Root para la validacion de ajustes del turno Metodo: oEventos_ObtenerSaldoTanqueAjusteTurno";
                    SWRegistros.WriteLine(DateTime.Now + "|Logueo|" + Comando + "|" + MensajeExcepcion);
                    SWRegistros.Flush();
                }
            }
            catch (Exception ex)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_ObtenerSaldoTanqueAjusteTurno: " + ex.Message;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }


        }
        public void ExisteComunicacionVeederRootReciboCombustible(ref bool ExisteComunicacion)
        {
            try
            {


                if (this.EsTCPIP)
                {
                    VerificarConexionReciboCombustible(ref ExisteComunicacion);
                }
                else
                {
                    VerificarConexionReciboCombustibleRS232(ref ExisteComunicacion);
                }


            }
            catch (Exception exec)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Verificando Comunicacion con VeederRoot Recibo Combustible|" + Comando + "|Excepcion|" + exec.Message);
                SWRegistros.Flush();
            }

        }

        //private void VerificarConexion()
        //{
        //    int x = 1;
        //    try
        //    {
        //        if (ClienteVeederRoot == null)
        //        {
        //            Boolean EsInicializado = false;
        //            while (!EsInicializado && x!=10)
        //            {
        //                try
        //                {
        //                    ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));

        //                    if (ClienteVeederRoot == null)
        //                    {
        //                        Thread.Sleep(3000);
        //                    }
        //                }
        //                catch
        //                {
        //                    Thread.Sleep(3000);
        //                }

        //                if (ClienteVeederRoot != null)
        //                {
        //                    EsInicializado = true;
        //                }

        //                x++;
        //            }
        //        }

        //        Boolean envioComando = false, estadoAnterior = true;
        //        if (!this.ClienteVeederRoot.Client.Connected)
        //        {
        //            estadoAnterior = false;
        //            SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento de reconexion");
        //            SWRegistros.Flush();

        //            try
        //            {
        //                ClienteVeederRoot.Client.BeginDisconnect(true, callBack, ClienteVeederRoot);
        //            }
        //            catch
        //            {
        //                Thread.Sleep(3000);
        //            }
        //        }
        //        else
        //        {
        //            estadoAnterior = true;
        //        }

        //        x = 1;
        //        while (!this.ClienteVeederRoot.Client.Connected && x!=10)
        //        {
        //            try
        //            {

        //                ClienteVeederRoot.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteVeederRoot);

        //                if (!envioComando)
        //                {
        //                    //oEventos.ReportarFalloenComunicacionVeederRoot();
        //                    envioComando = true;
        //                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Evento reportando la perdida de comunicacion");
        //                    SWRegistros.Flush();
        //                }

        //                if (!this.ClienteVeederRoot.Client.Connected)
        //                {
        //                    Thread.Sleep(3000);
        //                }

        //            }
        //            catch
        //            {
        //                Thread.Sleep(3000);
        //            }
        //            x++;
        //        }

        //        this.Stream = ClienteVeederRoot.GetStream();

        //        if (!estadoAnterior)
        //        {
        //            SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");
        //            SWRegistros.Flush();
        //        }
        //    }
        //    catch (Exception exec)
        //    {
        //        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);
        //        SWRegistros.Flush();
        //    }
        //}


        private void VerificarConexion()
        {
            int iReintento = 0;

            try
            {
                if (ClienteVeederRoot == null)
                {
                    Boolean EsInicializado = false;
                    while (!EsInicializado)
                    {
                        try
                        {
                            ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));

                            if (ClienteVeederRoot == null)
                            {
                                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|No inicializo - Ip: " + DireccionIP + " Puerto: " + Puerto);
                                SWRegistros.Flush();
                                Thread.Sleep(1500);
                            }
                        }
                        catch (Exception e)
                        {
                            SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla de inicializacion - Ip: " + DireccionIP + " Puerto: " + Puerto + " Mensaje: " + e.Message);
                            SWRegistros.Flush();
                            Thread.Sleep(1500);
                        }

                        if (ClienteVeederRoot != null)
                        {
                            SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);
                            SWRegistros.Flush();
                            EsInicializado = true;
                        }
                    }
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteVeederRoot.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistros.Flush();

                    try
                    {
                        ClienteVeederRoot.Client.BeginDisconnect(true, callBack, ClienteVeederRoot);

                    }

                    catch (Exception e)
                    {

                        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginDisconnect: " + e.Message);
                        SWRegistros.Flush();
                        Thread.Sleep(1500);
                    }
                }
                else
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");
                    SWRegistros.Flush();
                    estadoAnterior = true;
                }



                while (!this.ClienteVeederRoot.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistros.Flush();


                        ClienteVeederRoot.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteVeederRoot);
                        //ClienteVeederRoot.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteVeederRoot.Client.Connected)
                        {
                            Thread.Sleep(1500);
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {//Reintento de conexcion para el caso de Cruz roja

                        //SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect-Creando Socket: " + ex.Message);
                        //SWRegistros.Flush();
                        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect-Creando Socket: Abriendo nuevamente la conexcion");
                        SWRegistros.Flush();

                        AbrirSocketReintento();

                    }
                    catch (Exception)
                    {
                        //SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect: " + e.Message);
                        //SWRegistros.Flush();

                        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect: Abriendo nuevamente la conexcion");
                        SWRegistros.Flush();

                        AbrirSocketReintento();
                    }
                }

                this.Stream = ClienteVeederRoot.GetStream();

                if (!estadoAnterior)
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");
                    SWRegistros.Flush();
                }
            }
            catch (Exception exec)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);
                SWRegistros.Flush();
            }
        }


        void AbrirSocketReintento()
        {
            try
            {
                Thread.Sleep(1500);
                LimpiarVariableSocket();//Libero los recursos antes de iniciar una nueva conexcion con la veeder
                ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteVeederRoot.GetStream();
                if (this.ClienteVeederRoot.Client.Connected == true)
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");
                    SWRegistros.Flush();
                }
                else
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Conexion Cerrada");
                    SWRegistros.Flush();
                }


            }
            catch (Exception ex)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Falla AbrirSocketReintento  Creando Socket : " + ex.Message);
                SWRegistros.Flush();

            }

        }


        private void VerificarConexionReciboCombustible(ref bool ExisteComunicacion)
        {
            int i = 0;

            bool Band = false;
            try
            {
                if (ClienteVeederRoot == null)
                {

                    while (i <= 5 || Band == false)
                    {
                        try
                        {
                            ClienteVeederRoot = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));

                            if (ClienteVeederRoot == null)
                            {
                                Thread.Sleep(500);//Espero 1 segundo para intentar de nuevo
                            }
                        }
                        catch
                        {
                            Thread.Sleep(500);
                        }

                        if (ClienteVeederRoot != null)
                        {
                            Band = true;
                        }
                        i++;
                    }
                }
                else
                    ExisteComunicacion = true;


                if (ClienteVeederRoot == null)//si es null es porque no se pudo conectar con la veeder root y por tanto no hay comunicacion en el momento que se esta realizando la conexcion
                {
                    ExisteComunicacion = false;

                }


            Validar:
                if (ExisteComunicacion == true)
                {
                    if (EncuestaEnProceso == false)
                    {
                        PollingTimer.Stop();
                        if (ProcesoValidacionComunicacionVeederRoot())
                        {
                            SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|TCP:Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");
                            SWRegistros.Flush();
                            ExisteComunicacion = true;
                            Thread.Sleep(1500);
                        }
                        else
                        {
                            ExisteComunicacion = false;

                        }
                        //PollingTimer.Start();
                        ConfigurarTimer();
                        return;

                    }
                    else
                    {

                        //SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Validacion previa  al ajuste de tanques en turno o al recibo de combustible: Existe una encuestra en proceso se procede a esperar que termine");
                        //SWRegistros.Flush();

                        goto Validar;
                    }


                }
                else
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|TCP:NO Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");
                    SWRegistros.Flush();
                    ExisteComunicacion = false;
                }
            }
            catch (Exception exec)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Excepcion En Verificacion estado en recibo combustible|" + exec.Message);
                SWRegistros.Flush();
            }
        }


        private void VerificarConexionRS232()
        {
            try
            {
                Boolean EsInicializado = false;

                if (this.EsTCPIP == false)
                {

                    if (PuertoFAFNIR == null)
                    {

                        while (!EsInicializado)
                        {
                            try
                            {

                                PuertoFAFNIR = new SerialPort();

                                PuertoFAFNIR.PortName = Puerto;
                                PuertoFAFNIR.BaudRate = 9600;
                                PuertoFAFNIR.DataBits = 8;
                                PuertoFAFNIR.StopBits = StopBits.One;
                                PuertoFAFNIR.Parity = Parity.None;
                                PuertoFAFNIR.ReadBufferSize = 4096;
                                PuertoFAFNIR.WriteBufferSize = 4096;

                                //Abre el puerto COM de comunicación con Veeder Root
                                PuertoFAFNIR.Open();

                                if (PuertoFAFNIR.IsOpen == true)
                                {
                                    EsInicializado = true;
                                }
                                else
                                {
                                    Thread.Sleep(30000);
                                }

                            }
                            catch
                            {
                                Thread.Sleep(30000);
                            }
                        }

                    }
                    else
                    {
                        if (PuertoFAFNIR.IsOpen == false)
                        {
                            while (!EsInicializado)
                            {
                                try
                                {

                                    PuertoFAFNIR = new SerialPort();

                                    PuertoFAFNIR.PortName = Puerto;
                                    PuertoFAFNIR.BaudRate = 9600;
                                    PuertoFAFNIR.DataBits = 8;
                                    PuertoFAFNIR.StopBits = StopBits.One;
                                    PuertoFAFNIR.Parity = Parity.None;
                                    PuertoFAFNIR.ReadBufferSize = 4096;
                                    PuertoFAFNIR.WriteBufferSize = 4096;

                                    //Abre el puerto COM de comunicación con Veeder Root
                                    PuertoFAFNIR.Open();

                                    if (PuertoFAFNIR.IsOpen == true)
                                    {
                                        EsInicializado = true;
                                    }
                                    else
                                    {
                                        Thread.Sleep(30000);
                                    }

                                }
                                catch
                                {
                                    Thread.Sleep(30000);
                                }

                            }

                        }

                    }
                }

            }
            catch (Exception exec)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Conexion Veeder Serial Excepcion|" + exec.Message);
                SWRegistros.Flush();
            }
        }


        private void VerificarConexionReciboCombustibleRS232(ref bool ExisteComunicacion)
        {
            int i = 0;
            try
            {
                Boolean EsInicializado = false;
                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|RS232: Inicia validacion de conexcion " + "-- ExisteComunicacion=" + ExisteComunicacion.ToString());
                SWRegistros.Flush();

                if (this.EsTCPIP == false)
                {

                    if (PuertoFAFNIR == null)
                    {

                        while (i <= 10 || EsInicializado == false)
                        {
                            try
                            {
                                PuertoFAFNIR.PortName = Puerto;
                                PuertoFAFNIR.BaudRate = 9600;
                                PuertoFAFNIR.DataBits = 8;
                                PuertoFAFNIR.StopBits = StopBits.One;
                                PuertoFAFNIR.Parity = Parity.None;
                                PuertoFAFNIR.ReadBufferSize = 4096;
                                PuertoFAFNIR.WriteBufferSize = 4096;

                                //Abre el puerto COM de comunicación con Veeder Root
                                PuertoFAFNIR.Open();

                                if (PuertoFAFNIR.IsOpen == true)
                                {

                                    EsInicializado = true;
                                    ExisteComunicacion = true;

                                }
                                else
                                {
                                    Thread.Sleep(500);
                                    ExisteComunicacion = false;
                                }
                                i++;

                            }
                            catch (Exception ex)
                            {
                                SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|RS232: Error logeado:" + ex.Message + "-- ExisteComunicacion=" + ExisteComunicacion.ToString());
                                SWRegistros.Flush();
                                Thread.Sleep(500);
                            }
                        }

                    }
                    else
                    {
                        i = 0;
                        EsInicializado = false;
                        if (PuertoFAFNIR.IsOpen == false)
                        {


                            while (i <= 15 || EsInicializado == false)
                            {
                                try
                                {
                                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|RS232: Intento de apertura numero NO NULL :-- ExisteComunicacion=" + ExisteComunicacion.ToString());
                                    SWRegistros.Flush();


                                    PuertoFAFNIR = new SerialPort();

                                    PuertoFAFNIR.PortName = Puerto;
                                    PuertoFAFNIR.BaudRate = 9600;
                                    PuertoFAFNIR.DataBits = 8;
                                    PuertoFAFNIR.StopBits = StopBits.One;
                                    PuertoFAFNIR.Parity = Parity.None;
                                    PuertoFAFNIR.ReadBufferSize = 4096;
                                    PuertoFAFNIR.WriteBufferSize = 4096;

                                    //Abre el puerto COM de comunicación con Veeder Root
                                    PuertoFAFNIR.Open();

                                    if (PuertoFAFNIR.IsOpen == true)
                                    {


                                        EsInicializado = true;
                                        ExisteComunicacion = true;

                                    }
                                    else
                                    {
                                        Thread.Sleep(1000);
                                        ExisteComunicacion = false;
                                    }

                                }
                                catch (Exception ex)
                                {
                                    SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|RS232: Error logeado NO NULL:" + ex.Message + "-- ExisteComunicacion=" + ExisteComunicacion.ToString());
                                    SWRegistros.Flush();
                                    Thread.Sleep(1000);
                                }
                                i++;

                            }

                        }
                        else
                        {
                            ExisteComunicacion = true;
                        }

                    }

                Validar:
                    if (ExisteComunicacion == true)
                    {
                        if (EncuestaEnProceso == false)
                        {
                            ExisteComunicacion = true;
                            ConfigurarTimer();
                            return;


                        }
                        else
                        {
                            goto Validar;

                        }

                    }
                    else
                    {
                        ExisteComunicacion = false;
                        SWRegistros.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|R232:NO Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");
                        SWRegistros.Flush();
                    }

                }

            }
            catch (Exception exec)
            {
                SWRegistros.WriteLine(DateTime.Now + "|Conexion Veeder Serial Excepcion Recibo Combustible|" + exec.Message);
                SWRegistros.Flush();
            }
        }

        private static void CallBackMethod(IAsyncResult asyncresult)
        {

        }

        //RECIBE ENVENTO PARA OBTENER DATOS PARA REPORTAR INVENTARIO A PETICION DE TERMINAL
        private void oEventos_InformarStocksTanques()
        {
            try
            {
                SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|oEventos_InformarStocksTanques|Recibe peticion para reportar Inventario Petición MR");
                SWRegistros.Flush();



                ReportarInventario = true;

                //Revisa si hay una encuesta en proceso
                if (!EncuestaEnProceso)
                {
                    //Detiene el Timer
                    PollingTimer.Stop();

                    if (EsTCPIP)
                    {
                        VerificarConexion();
                    }
                    else
                    {
                        VerificarConexionRS232();
                    }


                    //Envia comando y recibe respuesta de INVENTARIO
                    ProcesoObtencionReporteVariables();//OK

                    //Una vez terminada la encuesta, reinicia el Timer
                    ConfigurarTimer();
                    //PollingTimer.Start();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanques: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR -Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(ArchivoRegistros);
                if (FileInf.Length > 30000000)
                {
                    SWRegistros.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistros = Application.StartupPath + "/LogueoFAFNIR/" + "FAFNIR - Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                    SWRegistros = File.AppendText(ArchivoRegistros);
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        //RECIBE ENVENTO PARA OBTENER DATOS PARA REPORTAR INVENTARIO POR CIERRE DE TURNO
        private void oEventos_InformarStocksTanquesCierreTurno(ref int idTurno)
        {
            try
            {
                SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Recibe peticion para reportar Inventario Petición Cierre Turno");
                SWRegistros.Flush();
                int i = 0;
                //Valido si hay una encuenta en curso y espero a que finalize
                while (EncuestaEnProceso && i <= 10)//JDT encuenta de variables de medicion ---  se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Esperando Finalizacion de Encuesta Periodica");
                    SWRegistros.Flush();
                    i++;
                    Thread.Sleep(500);

                }

                if (i >= 9)
                {
                    EncuestaEnProceso = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Finaliza espera periodica EncuestaEnProceso: " + EncuestaEnProceso.ToString());
                    SWRegistros.Flush();
                }              


                ReportarInventarioTurno = true;

                IdTurno = idTurno;

                //Revisa si hay una encuesta en proceso
                if (!EncuestaEnProceso)
                {
                    //Detiene el Timer
                    PollingTimer.Stop();

                    if (EsTCPIP)
                    {
                        VerificarConexion();
                    }
                    else
                    {
                        VerificarConexionRS232();
                    }

                    //Envia comando y recibe respuesta de INVENTARIO
                    ProcesoObtencionReporteVariables();//OK

                    //Una vez terminada la encuesta, reinicia el Timer
                    //PollingTimer.Start();
                    ConfigurarTimer();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanquesCierreTurno: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        //RECIBE ENVENTO PARA OBTENER DATOS PARA REPORTAR INVENTARIO POR CIERRE DE TURNO DEL SERVICIO WINDOWS PARA VALIDACIONES DE AJUSTE DE TANQUES
        private void oEventos_InformarStocksTanquesCierreTurnoServicio(ref int idTurno)
        {
            try
            {
                SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Recibe peticion para reportar Inventario Petición Cierre Turno");
                SWRegistros.Flush();
                int i = 0;
                //Valido si hay una encuenta en curso y espero a que finalize
                while (EncuestaEnProceso && i <= 10)//JDT encuenta de variables de medicion ---  se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                {
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Esperando Finalizacion de Encuesta Periodica");
                    SWRegistros.Flush();
                    i++;
                    Thread.Sleep(500);

                }

                if (i >= 9)
                {
                    EncuestaEnProceso = false;
                    SWRegistros.WriteLine(DateTime.Now + "|Evento|" + Comando + "|Finaliza espera periodica EncuestaEnProceso: " + EncuestaEnProceso.ToString());
                    SWRegistros.Flush();
                }              

                IdTurnoTemp = idTurno;

                //Revisa si hay una encuesta en proceso
                if (!EncuestaEnProceso)
                {
                    //Detiene el Timer
                    PollingTimer.Stop();

                    if (EsTCPIP)
                    {
                        VerificarConexion();
                    }
                    else
                    {
                        VerificarConexionRS232();
                    }

                    //Envia comando y recibe respuesta de INVENTARIO
                    ProcesoObtencionReporteVariables(true);//ok

                    //Una vez terminada la encuesta, reinicia el Timer
                    ConfigurarTimer();
                    //PollingTimer.Start();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanquesCierreTurno: " + Excepcion;
                SWRegistros.WriteLine(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                SWRegistros.Flush();
            }
        }

        #endregion
    }

}
