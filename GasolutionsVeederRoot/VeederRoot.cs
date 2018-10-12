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

namespace POSstation.Protocolos.VeederRoot
{
    public class VeederRoot
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


        SerialPort PuertoVeederRoot = new SerialPort();
        TcpClient ClienteVeederRoot;
        NetworkStream Stream;
        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        private enum ComandoVeederRoot
        {
            Inventario,
            set_Time_Day
        }
        ComandoVeederRoot Comando;

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
        private string FrecuenciaVeederEncuesta;

        #endregion

        #region PUNTO DE ARRANQUE DE LA CLASE


        public VeederRoot(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<int, Tanque> Tanques, ref SharedEventsFuelStation.CMensaje OEventosAutorizador, string Frecuencia)
        {
            try
            {



                if (!Directory.Exists(Application.StartupPath + "/LogueoVeederRoot"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoVeederRoot/");
                }
                PollingTimer = new System.Timers.Timer(30000);

                ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";


                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

                ////Escribe el encabezado del archivo plano
                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //    " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2013-03-27 1202  Frecuencia:  " + Frecuencia);
                //Escribe el encabezado del archivo plano
                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //    " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2013-03-27 0811  Frecuencia:  " + Frecuencia);//Cambio en en el tiempo de espera de la lectura del buffer TCP
                //

                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2013-03-29 0830  Frecuencia:  " + Frecuencia);//Reintento de solictud de inventario en caso de falla en la comunicacion
                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2013-04-06 0900  Frecuencia:  " + Frecuencia);//Reintento de solictud de inventario en caso de que no envie bytes la veeder y ademas validacion en el proceso de reportar Variables
                //
                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " Version:2017-06-15 1502  Frecuencia:  " + Frecuencia);//Reintento de conexcion para el caso de Cruz roja y JDT encuenta de variables de medicion
                //

                //                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 26/06/2017 - 0925 Frecuencia:  " + Frecuencia); /// se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                //                


                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 24/07/2018 - 1548"); /// se amplia el timeout a 4000 y se coloca el almacenarenarchivo



                //Arma la Estructura de Tanques y sus propiedades
                TanquesVR = new Dictionary<int, Tanque>();
                TanquesVR = Tanques;


                SWRegistros.Write(DateTime.Now + "|Inicio|Configuracion|");
                foreach (Tanque Tanque in TanquesVR.Values)
                    SWRegistros.Write("Tanque: " + Tanque.TankNumber + " - ");

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
                    PuertoVeederRoot.PortName = Puerto;
                    PuertoVeederRoot.BaudRate = 9600;
                    PuertoVeederRoot.DataBits = 8;
                    PuertoVeederRoot.StopBits = StopBits.One;
                    PuertoVeederRoot.Parity = Parity.None;
                    PuertoVeederRoot.ReadBufferSize = 4096;
                    PuertoVeederRoot.WriteBufferSize = 4096;

                    //Abre el puerto COM de comunicación con Veeder Root
                    PuertoVeederRoot.Open();
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
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

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
                AlmacenarEnArchivo(DateTime.Now + "|Metodo|" + "LimpiarVariableSocket: " + "Mensaje: " + ex.Message);

            }

        }

        void ConfigurarTimer()
        {
            double TiemprElapse = 0;
            try
            {
                AlmacenarEnArchivo(DateTime.Now + "|Entro a la Configuracion  del Orquestador|Intervalo: " + Intervalo.ToString());

                if (DateTime.Now > FechaFinal)
                {
                    FechaFinal = DateTime.Now.AddMinutes(Convert.ToDouble(FrecuenciaVeederEncuesta));
                    FechaInicial = DateTime.Now;
                    Intervalo = System.Math.Abs(FechaFinal.Subtract(FechaInicial).TotalMilliseconds);
                    //TiemprElapse = Intervalo;

                    if (Intervalo <= 0)
                    {
                        Intervalo = 30000;
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

                AlmacenarEnArchivo(DateTime.Now + "|Salio de la Configuracion  del Orquestador|Intervalo: " + Intervalo.ToString());

                AlmacenarEnArchivo(DateTime.Now + "|Configuracion Orquestador|Intervalo: " + Intervalo.ToString());

            }
            catch (Exception Excepcion)
            {
                try
                {
                    PollingTimer.Dispose();
                    PollingTimer = null;
                    PollingTimer = new System.Timers.Timer(Convert.ToDouble(30000));
                    PollingTimer.Elapsed += new ElapsedEventHandler(PollingTimerEvent);
                    PollingTimer.AutoReset = true;
                    PollingTimer.Start();
                }
                catch (Exception)
                {


                }
                string MensajeExcepcion = "Excepcion en el metodo ConfigurarTimer: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }

        }


        public VeederRoot(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<int, Tanque> Tanques)
        {
            try
            {

                PollingTimer = new System.Timers.Timer(30000);
                if (!Directory.Exists(Environment.CurrentDirectory + "/LogueoVeederRoot"))
                {
                    Directory.CreateDirectory(Environment.CurrentDirectory + "/LogueoVeederRoot/");
                }

                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";


                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";




                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 24/07/2018 - 1548"); /// se amplia el timeout a 4000 y se coloca el almacenarenarchivo



                //Arma la Estructura de Tanques y sus propiedades
                TanquesVR = new Dictionary<int, Tanque>();
                TanquesVR = Tanques;

                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|");
                foreach (Tanque Tanque in TanquesVR.Values)
                    AlmacenarEnArchivo("Tanque: " + Tanque.TankNumber + " - ");


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
                    PuertoVeederRoot.PortName = Puerto;
                    PuertoVeederRoot.BaudRate = 9600;
                    PuertoVeederRoot.DataBits = 8;
                    PuertoVeederRoot.StopBits = StopBits.One;
                    PuertoVeederRoot.Parity = Parity.None;
                    PuertoVeederRoot.ReadBufferSize = 4096;
                    PuertoVeederRoot.WriteBufferSize = 4096;

                    //Abre el puerto COM de comunicación con Veeder Root
                    PuertoVeederRoot.Open();
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
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }


        public VeederRoot(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<int, Tanque> Tanques, ref SharedEventsFuelStation.CMensaje OEventosAutorizador)
        {
            try
            {

                PollingTimer = new System.Timers.Timer(30000);
                if (!Directory.Exists(Application.StartupPath + "/LogueoVeederRoot"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoVeederRoot/");

                }



                ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";



                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Veeder Root)
                ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";



                ////Escribe el encabezado del archivo plano
                //AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP VERSION 2012-11-27: " + EsTCPIP +
                //    " - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto);
                //

                //                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
                //" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 26/06/2017 - 0924  Frecuencia:  " + Intervalo.ToString()); /// se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                //                



                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|Comunicacion TCP/IP: " + EsTCPIP +
" - Direccion IP: " + DireccionIP + " - Puerto: " + Puerto + " - Ver: 1.0 - 24/07/2018 - 1548"); /// se amplia el timeout a 4000 y se coloca el almacenarenarchivo




                //Arma la Estructura de Tanques y sus propiedades
                TanquesVR = new Dictionary<int, Tanque>();
                TanquesVR = Tanques;

                AlmacenarEnArchivo(DateTime.Now + "|Inicio|Configuracion|");
                foreach (Tanque Tanque in TanquesVR.Values)
                    AlmacenarEnArchivo("Tanque: " + Tanque.TankNumber + " - ");



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
                    PuertoVeederRoot.PortName = Puerto;
                    PuertoVeederRoot.BaudRate = 9600;
                    PuertoVeederRoot.DataBits = 8;
                    PuertoVeederRoot.StopBits = StopBits.One;
                    PuertoVeederRoot.Parity = Parity.None;
                    PuertoVeederRoot.ReadBufferSize = 4096;
                    PuertoVeederRoot.WriteBufferSize = 4096;

                    //Abre el puerto COM de comunicación con Veeder Root
                    PuertoVeederRoot.Open();
                }

                //Instancia los eventos de los objetos Timer
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
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }

        #endregion

        #region CONVERSIÓN Y ALMACENAMIENTO DE VARIABLES

        //ALMACENA EN EL DICCIONARIO DE TANQUES LOS VALORES OBTENIDO EN LA TRAMA
        private void AlmacenarInventario()
        {
            try
            {

                int j = 17;
                do
                {
                    CodigoTanque = Convert.ToInt16(System.Text.Encoding.ASCII.GetString(TramaRx, j, 2), 16);

                    if (TanquesVR.ContainsKey(CodigoTanque))
                    {
                        TanquesVR[CodigoTanque].TankNumber = CodigoTanque;
                        TanquesVR[CodigoTanque].ProductCode = Convert.ToInt16(System.Text.Encoding.ASCII.GetString(TramaRx, j + 2, 1), 16);

                        int NumeroVariables = Convert.ToInt16(System.Text.Encoding.ASCII.GetString(TramaRx, j + 7, 2), 16);
                        if (NumeroVariables == 7)
                        {
                            TanquesVR[CodigoTanque].Volume = ObtenerValor(j + 9, 8);
                            TanquesVR[CodigoTanque].TCVolume = ObtenerValor(j + 17, 8);
                            TanquesVR[CodigoTanque].Ullage = ObtenerValor(j + 25, 8);
                            TanquesVR[CodigoTanque].Heigh = ObtenerValor(j + 33, 8);
                            TanquesVR[CodigoTanque].Water = ObtenerValor(j + 41, 8);
                            TanquesVR[CodigoTanque].Temperature = ObtenerValor(j + 49, 8);
                            TanquesVR[CodigoTanque].WaterVolume = ObtenerValor(j + 57, 8);
                            ExisteValorInventario = true;

                        }
                    }
                    j += 65;
                } while (TramaRx[j] != Convert.ToByte('&') && TramaRx[j + 1] != Convert.ToByte('&'));



                //se obtiene la fecha actual configurada en la VR DCF
                FechaHora = Convert.ToString(System.Text.Encoding.ASCII.GetString(TramaRx, 5, 16));

                AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Fecha de VR actual  = " + FechaHora); // Borrar solo para inspección 


                //
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo AlmacenarInventario: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

                ExisteValorInventario = true;
            }
        }


        private void AlmacenarEnArchivo(string Mensaje)
        {

            try
            {
                if (!Directory.Exists(Application.StartupPath + "/LogueoVeederRoot"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoVeederRoot/");
                }

                ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

                if (SWRegistros == null)
                    SWRegistros = File.AppendText(ArchivoRegistros);

                lock (SWRegistros)
                {
                    FileInfo FileInf = new FileInfo(ArchivoRegistros);
                    if (FileInf.Length > 30000000)
                    {
                        SWRegistros.Close();
                        //Crea archivo para almacenar inconsistencias en el proceso logico
                        ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                        SWRegistros = File.AppendText(ArchivoRegistros);
                    }

                    SWRegistros.WriteLine(Mensaje);
                    SWRegistros.Flush();

                }

            }
            catch (IOException ex)
            {
                try
                {
                    try
                    {
                        SWRegistros.Close();
                    }
                    catch (Exception)
                    {

                    }
                    SWRegistros = null;

                }
                catch (Exception)
                {


                }
            }
            catch (ObjectDisposedException ex)
            {
                try
                {
                    try
                    {
                        SWRegistros.Close();
                    }
                    catch (Exception)
                    {

                    }
                    SWRegistros = null;

                }
                catch (Exception)
                {


                }
            }
            catch (Exception ex)
            {
            }
        }


        private void AlmacenarEnArchivoTrama(string Mensaje)
        {

            try
            {
                if (!Directory.Exists(Application.StartupPath + "/LogueoVeederRoot"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoVeederRoot/");
                }

                ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

                if (SWTramas == null)
                    SWTramas = File.AppendText(ArchivoTramas);

                lock (SWTramas)
                {
                    FileInfo FileInf = new FileInfo(ArchivoTramas);
                    if (FileInf.Length > 50000000)
                    {
                        SWTramas.Close();
                        ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                        SWTramas = File.AppendText(ArchivoTramas);
                    }
                    SWTramas.WriteLine(Mensaje);
                    SWTramas.Flush();
                }

            }
            catch (ObjectDisposedException ex)
            {
                try
                {
                    try
                    {
                        SWTramas.Close();
                    }
                    catch (Exception)
                    {

                    }
                    SWTramas = null;

                }
                catch (Exception)
                {


                }
            }
            catch (IOException ex)
            {
                try
                {
                    try
                    {
                        SWTramas.Close();
                    }
                    catch (Exception)
                    {

                    }
                    SWTramas = null;

                }
                catch (Exception)
                {


                }
            }
            catch (Exception ex)
            {
            }
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
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
                return 0;
            }
        }

        #endregion

        #region COMUNICACION CON VEEDER ROOT: ENVIO, RECEPCION Y VALIDACIÓN DE INTEGRIDAD DE TRAMAS

        //INICIA EL PROCESO DE ENVIO DE COMANDO Y RECEPCION DE INFORMACION DE LA VEEDER ROOT
        private bool ProcesoEnvioComando(ComandoVeederRoot Comando)
        {
            try
            {
                this.Comando = Comando;
                ArmarTramaTx();

                bool ComandoExitoso = true;
                int i = 0;
                while (EnvioComando && i <= 8)
                {
                    i++;
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso Envio Comando: " + EnvioComando.ToString() + " Espera obligatoria");

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
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

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
                    case ComandoVeederRoot.Inventario:
                        //{SOH i 2 0 1 T T } TT = 00 Todos los tanques
                        TramaTx = System.Text.Encoding.ASCII.GetBytes(Convert.ToChar(0x01) + "i20100");// + CodigoTanque.ToString().PadLeft(2, '0'));
                        break;

                    case ComandoVeederRoot.set_Time_Day:
                        //{SOH S50100YYMMDDHHmm}

                        string YYMMDDHHmm = (DateTime.Now.Year.ToString().Remove(0, 2) + DateTime.Now.Month.ToString().PadLeft(2, '0') + DateTime.Now.Day.ToString().PadLeft(2, '0') +
                                         DateTime.Now.Hour.ToString().PadLeft(2, '0') + DateTime.Now.Minute.ToString().PadLeft(2, '0'));


                        TramaTx = System.Text.Encoding.ASCII.GetBytes(Convert.ToChar(0x01) + "s50100" + YYMMDDHHmm);

                        AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Fecha a enviar a la VR = " + YYMMDDHHmm); // Borrar solo para inspección 




                        break;





                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ArmarTramaTx: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }
        //Comentada por prueba con la Veeder Root
        ////CONFIGURACION DE LOS PARAMETROS RESPECTIVOS Y ENVÍA PARA COMUNICAR POR UN SOCKET TCP/IP
        //private bool EnviarTramaTCPIP()
        //{
        //    try
        //    {
        //        Stream.Write(TramaTx, 0, TramaTx.Length);
        //        Stream.Flush();

        //        //Loguea en el archivo plano la trama enviada
        //       AlmacenarEnArchivoTrama(DateTime.Now + "|Tx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaTx));
        //       


        //        Stream.ReadTimeout = 7000;
        //        Stream.Flush();
        //        if (Stream.DataAvailable)
        //        {
        //            //Almacena la información recibida en TramaRx
        //            byte[] TramaRxTemporal = new byte[1000];
        //            int BytesRecibidos = Stream.Read(TramaRxTemporal, 0, TramaRxTemporal.Length);

        //            TramaRx = new byte[BytesRecibidos];
        //            for (int i = 0; i < TramaRx.Length; i++)
        //                TramaRx[i] = TramaRxTemporal[i];

        //            //Loguea en el archivo plano la trama recibiida
        //           AlmacenarEnArchivoTrama(DateTime.Now + "|Rx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaRx));
        //           


        //            return true;
        //        }
        //        else
        //        {
        //            AlmacenarEnArchivo(DateTime.Now + "|Error|" + Comando + "|Veeder Root no envio informacion");
        //            

        //            return false;
        //        }
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepcion en el metodo EstablecerComunicacionTCPIP: " + Excepcion;
        //        AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);
        //        
        //        return false;
        //    }
        //}



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
                    Thread.Sleep(4000);

                }
                catch (System.IO.IOException)//Si genera error lo capturo, espero y reenvio el comando
                {
                    try
                    {
                        AlmacenarEnArchivo(DateTime.Now + "|Reintento de envio de comando");

                        Thread.Sleep(1500);
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
                        AlmacenarEnArchivo(DateTime.Now + "|Reintento de envio de comando");

                        Thread.Sleep(1500);
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
                        AlmacenarEnArchivo(DateTime.Now + "|Reintento de envio de comando");

                        Thread.Sleep(1500);
                        VerificarConexion();
                        Stream.Write(TramaTx, 0, TramaTx.Length);
                        Stream.Flush();
                    }
                    catch (Exception)
                    {

                    }

                }



                //Loguea en el archivo plano la trama enviada
                AlmacenarEnArchivoTrama(DateTime.Now + "|Tx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaTx));

                Stream.ReadTimeout = 6000;
                AlmacenarEnArchivo(DateTime.Now + "|Antes de leer TCP-IP|");

                //  Thread.Sleep(3000);

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

                        AlmacenarEnArchivo(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());

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

                        AlmacenarEnArchivo(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());

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

                        AlmacenarEnArchivo(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());

                        BytesRecibidos = 0;
                    }

                }


                AlmacenarEnArchivo(DateTime.Now + "|Despues de leer TCP-IP -Bytes Recibidos de la Veeder Root:  " + BytesRecibidos.ToString());


                while (NumReintentos <= 20 && BytesRecibidos <= 0)//Reintento de reconexcion en caso que  la veeder responda con 0 Bytes
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Reintento de Peticion #: " + NumReintentos.ToString() + "|");


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

                            AlmacenarEnArchivo(DateTime.Now + "|No respondio al comando:  " + BytesRecibidos.ToString());

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
                    AlmacenarEnArchivo(DateTime.Now + "|TramaRx Cantidad Bytes Recibidos:  " + BytesRecibidos.ToString());
                    AlmacenarEnArchivoTrama(DateTime.Now + "|Rx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaRx));

                    EnvioComando = false;
                    return true;
                }
                else
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Error|" + Comando + "|Veeder Root no envio informacion");

                    EnvioComando = false;
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo EstablecerComunicacionTCPIP: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

                EnvioComando = false;
                return false;
            }
        }




        //CONFIGURACION DE LOS PARAMETROS PARA COMUNICAR POR UNA CONEXIÓN RS232
        private bool EnviarTramaRS232()
        {
            int contador = 0;
            EnvioComando = true;
            try
            {
                //Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                PuertoVeederRoot.DiscardOutBuffer();
                PuertoVeederRoot.DiscardInBuffer();

                //Loguea en el archivo plano la trama TX
                AlmacenarEnArchivoTrama(DateTime.Now + "|Tx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaTx));


                //Escribe en el puerto el comando a Enviar.
                PuertoVeederRoot.Write(TramaTx, 0, TramaTx.Length);
                Thread.Sleep(3000);

                int BytesRecibidos = PuertoVeederRoot.BytesToRead;

                //comprobar que el buffer esta lleno.DCF


                while ((BytesRecibidos != PuertoVeederRoot.BytesToRead) && contador == 15) //Si el buffer se esta llenando entra y espera 20 ms mas y crea la dimensión del vector 
                {
                    System.Threading.Thread.Sleep(200);
                    BytesRecibidos = PuertoVeederRoot.BytesToRead;
                    System.Threading.Thread.Sleep(200);

                    AlmacenarEnArchivo(DateTime.Now + "|Estado|" + Comando + "|Comprobar # BytesRecibidos = " + BytesRecibidos);

                    contador++;
                }

                if (BytesRecibidos > 0)
                {
                    TramaRx = new byte[BytesRecibidos];

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    PuertoVeederRoot.Read(TramaRx, 0, BytesRecibidos);

                    //Loguea en el archivo plano la trama recibiida
                    AlmacenarEnArchivoTrama(DateTime.Now + "|Rx|" + System.Text.ASCIIEncoding.ASCII.GetString(TramaRx));

                    EnvioComando = false;

                    return true;
                }
                else
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Error|" + Comando + "|Veeder Root no envio informacion");

                    EnvioComando = false;
                    return false;
                }


            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo EnviarTramaRS23: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

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
                if (TramaRx[0] != 0x01)
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Inconsistencia|" + Comando + "|Encabezado errado: " + TramaRx[0]);

                    return false;
                }
                else
                {
                    //Valida Fin de trama
                    if (TramaRx[TramaRx.Length - 1] != 0x03)
                    {
                        AlmacenarEnArchivo(DateTime.Now + "|Inconsistencia|" + Comando + "|Fin de trama errado: " + TramaRx[TramaRx.Length - 1]);

                        return false;
                    }
                    else
                    {
                        //Valida CheckSum
                        long ChecksumTramaRx = Convert.ToInt64(System.Text.Encoding.ASCII.GetString(TramaRx, TramaRx.Length - 5, 4), 16);
                        long ChecksumCalculado = Checksum();
                        if (ChecksumTramaRx != ChecksumCalculado)
                        {
                            AlmacenarEnArchivo(DateTime.Now + "|Inconsistencia|" + Comando + "|Checksum Recibido: " + ChecksumTramaRx +
                                " - Checksum Calculado: " + ChecksumCalculado);

                            return false;
                        }
                        else
                            return true;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo ValidarIntegridadTramaRx: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

                return false;
            }
        }

        //CALCULA EL CHECKSUM QUE DEBE RECIBIR EN LA TRAMA DE RECEPCION
        private long Checksum()
        {
            try
            {
                long Checksum = 0;
                for (int i = 0; i < TramaRx.Length; i++)
                {
                    if (i < TramaRx.Length - 5)
                        Checksum += TramaRx[i];
                }
                return ((Checksum ^ 0xFFFF) + 1) & 0xFFFF;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el metodo Checksum: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

                return 0;
            }
        }

        #endregion

        #region COMUNICACION CON AUTORIZADOR (CORE)



        private bool ProcesoValidacionComunicacionVeederRoot()
        {
            bool ProcesoOk = false;
            try
            {
                AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inicia proceso de Validacion Comunicacion VeederRoot:");
                ProcesoOk = ProcesoEnvioComando(ComandoVeederRoot.Inventario);
                AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Envio comando a VeederRoot respuesta : " + ProcesoOk.ToString());

                if (ProcesoOk)
                {

                    AlmacenarInventario();

                    //Declara la coleccion
                    SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                    SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Proceso de Validacion Comunicacion VeederRoot: Inicia a registrar la informacion de los tanques");

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

                        //    AlmacenarEnArchivo(
                        //                "   Tank: " + oTanque.TankNumber +
                        //                " - Product: " + oTanque.ProductCode +
                        //                " - Volume: " + oTanque.Volume.ToString("N2") +
                        //                " - TCVolume: " + oTanque.TCVolume.ToString("N2") +
                        //                " - Ullage: " + oTanque.Ullage.ToString("N2") +
                        //                " - Heigh: " + oTanque.Heigh.ToString("N2") +
                        //                " - Water: " + oTanque.Water.ToString("N2") +
                        //                " - Temperature: " + oTanque.Temperature.ToString("N2") +
                        //                " - WaterVolume: " + oTanque.WaterVolume.ToString("N2"));
                        //
                    }


                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Se registran las variables en proceso de Validacion Comunicacion VeederRoot");

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en ProcesoValidacionComunicacionVeederRoot: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
            return ProcesoOk;
        }

        //PROCESO PARA OBTENCION DE VARIABLES DE VEEDER ROOT    
        private void ProcesoObtencionReporteVariables()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoVeederRoot.Inventario))
                {

                    AlmacenarInventario();

                    //Declara la coleccion
                    SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                    SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");

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

                        AlmacenarEnArchivo(
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
                        AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición MR");

                    }
                    //Si se requiere enviar el inventario por Cierre de Turno
                    else if (ReportarInventarioTurno)
                    {
                        oEventos.SolicitarEnviarInformacionTanquesCierreTurno(ref RedTanquesInventario, ref IdTurno);
                        ReportarInventarioTurno = false;
                        AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición Cierre de Turno");

                    }

                    //oEventos.SolicitarReportarAlarmasVariablesTanques(ref RedTanquesVariables);
                    //AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                    //
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }


        private void ProcesoReportarVariablesdeMedicionPeriodicamente()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoVeederRoot.Inventario))
                {

                    AlmacenarInventario();

                    //Declara la coleccion
                    SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                    SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|INCIA PROCESO DE REPORTE DE VARIABLES DE MEDICION:");
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|**********************************************************************************************************************************************************");

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

                        AlmacenarEnArchivo(
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
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");
                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|FINALIZA PROCESO DE REPORTE DE VARIABLES DE MEDICION:");
                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|**********************************************************************************************************************************************************");

                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }

        private void ProcesoObtencionReporteVariables(bool EsTurno)
        {
            try
            {
                if (ProcesoEnvioComando(ComandoVeederRoot.Inventario))
                {

                    AlmacenarInventario();

                    //Declara la coleccion
                    SharedEventsFuelStation.ColTanques RedTanquesInventario = new SharedEventsFuelStation.ColTanques();
                    SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inventario:");

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

                        AlmacenarEnArchivo(
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
                        AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Petición Cierre de Turno");

                    }

                    //oEventos.SolicitarReportarAlarmasVariablesTanques(ref RedTanquesVariables);
                    //AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Reporta Inventario Periodico");
                    //
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObtencionEnvioDatosVeederRoot: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }
        #endregion



        private void ObetenerStockValidacionCierreTurno(ref SharedEventsFuelStation.ColTanques Tanques)
        {
            try
            {
                //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para inicar proceso de stock:");
                //
                if (EsTCPIP)
                {
                    //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para verificar concexcion IP:");
                    //
                    VerificarConexion();
                }
                else
                {
                    //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo de la veeder root para verificar concexcion rc232:");
                    //
                    VerificarConexionRS232();
                }


                if (Tanques == null)
                {
                    Tanques = new SharedEventsFuelStation.ColTanques();
                    //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo para iniciar la colecion:");
                    //
                }

                if (ProcesoEnvioComando(ComandoVeederRoot.Inventario))
                {
                    //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al metodo para Tomar valores de la veeder");
                    //


                    AlmacenarInventario();

                    //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Finalizo al metodo para Tomar valores de la veeder");
                    //

                    //Declara la coleccion                   
                    // SharedEventsFuelStation.ColTanques RedTanquesVariables = new SharedEventsFuelStation.ColTanques();
                    bool IsTankActive = true;

                    AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Inventario Validacion Cierre Tanques:");

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


                        AlmacenarEnArchivo("Saldo de Tanques para Validacion en Ajustes por Turno" +
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



                }




            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento ObetenerStockValidacionCierreTurno: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }

        #region EVENTOS DE LA CLASE

        //SE EJECUTA CADA PERIODO DE TIEMPO
        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void PollingTimerEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                //Setea variable que indica que ha comenzado la encuesta
                EncuestaEnProceso = true;

                //Se detiene el timer para realizar el respectivo proceso de encuesta
                PollingTimer.Stop();

                ////Set_Time_Day: DCF 06-10-2012 1218
                //if (!SetDayTime)
                //{
                //    ProcesoEnvioComando(ComandoVeederRoot.set_Time_Day);
                //    SetDayTime = true;


                //    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Se envía la Hora y fecha a la  VeederRoot");
                //    
                //}



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


                //Indica que ya finalizó el proceso
                EncuestaEnProceso = false;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento PollingTimerEvent: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

                EncuestaEnProceso = false;
            }
            finally
            {
                ConfigurarTimer();
            }

        }

        public void oEventos_ObtenerSaldoTanqueAjusteTurno(ref SharedEventsFuelStation.ColTanques Tanques)
        {
            int i = 0;
            try
            {
                //AlmacenarEnArchivo(DateTime.Now + "|Proceso|" + Comando + "|Entrando al evento para toma de datos de la veeder root en la validacion");
                //

                while ((i <= 20) || ExisteValorInventario != true)
                {
                    ObetenerStockValidacionCierreTurno(ref Tanques);
                    i++;
                    System.Threading.Thread.Sleep(300);
                    if (ExisteValorInventario == true)
                    {
                        break;
                    }

                }
                ExisteValorInventario = false;

                if (i == 10)
                {
                    string MensajeExcepcion = "No se pudo obetener el inventario de la Veeder Root para la validacion de ajustes del turno Metodo: oEventos_ObtenerSaldoTanqueAjusteTurno";
                    AlmacenarEnArchivo(DateTime.Now + "|Logueo|" + Comando + "|" + MensajeExcepcion);

                }
            }
            catch (Exception ex)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_ObtenerSaldoTanqueAjusteTurno: " + ex.Message;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

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
                AlmacenarEnArchivo(DateTime.Now + "|Verificando Comunicacion con VeederRoot Recibo Combustible|" + Comando + "|Excepcion|" + exec.Message);

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
        //            AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento de reconexion");
        //            

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
        //                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Evento reportando la perdida de comunicacion");
        //                    
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
        //            AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");
        //            
        //        }
        //    }
        //    catch (Exception exec)
        //    {
        //        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);
        //        
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
                                AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|No inicializo - Ip: " + DireccionIP + " Puerto: " + Puerto);

                                Thread.Sleep(1500);
                            }
                        }
                        catch (Exception e)
                        {
                            AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Falla de inicializacion - Ip: " + DireccionIP + " Puerto: " + Puerto + " Mensaje: " + e.Message);

                            Thread.Sleep(1500);
                        }

                        if (ClienteVeederRoot != null)
                        {
                            AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Inicializada - Ip: " + DireccionIP + " Puerto: " + Puerto);

                            EsInicializado = true;
                        }
                    }
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteVeederRoot.Client.Connected)
                {
                    estadoAnterior = false;
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");


                    try
                    {
                        ClienteVeederRoot.Client.BeginDisconnect(true, callBack, ClienteVeederRoot);

                    }

                    catch (Exception e)
                    {

                        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginDisconnect: " + e.Message);

                        Thread.Sleep(1500);
                    }
                }
                else
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");

                    estadoAnterior = true;
                }



                while (!this.ClienteVeederRoot.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());



                        ClienteVeederRoot.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteVeederRoot);
                        //ClienteVeederRoot.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteVeederRoot.Client.Connected)
                        {
                            Thread.Sleep(1500);
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {//Reintento de conexcion para el caso de Cruz roja

                        //AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect-Creando Socket: " + ex.Message);
                        //
                        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect-Creando Socket: Abriendo nuevamente la conexcion");


                        AbrirSocketReintento();

                    }
                    catch (Exception)
                    {
                        //AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Falla BeginConnect: " + e.Message);
                        //

                        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|BeginConnect: Abriendo nuevamente la conexcion");


                        AbrirSocketReintento();
                    }
                }

                this.Stream = ClienteVeederRoot.GetStream();

                if (!estadoAnterior)
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Reconexion establecida");

                }
            }
            catch (Exception exec)
            {
                AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Excepcion|" + exec.Message);

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
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Conexion Abierta");

                }
                else
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Conexion Cerrada");

                }


            }
            catch (Exception ex)
            {
                AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Falla AbrirSocketReintento  Creando Socket : " + ex.Message);


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
                        //if (ProcesoValidacionComunicacionVeederRoot())
                        //{
                        //    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|TCP:Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");

                        ExisteComunicacion = true;
                        //    Thread.Sleep(1500);
                        //}
                        //else
                        //{
                        //    ExisteComunicacion = false;

                        //}
                        //PollingTimer.Start();
                        ConfigurarTimer();
                        return;

                    }
                    else
                    {

                        //AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Validacion previa  al ajuste de tanques en turno o al recibo de combustible: Existe una encuestra en proceso se procede a esperar que termine");
                        //

                        goto Validar;
                    }


                }
                else
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|TCP:NO Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");

                    ExisteComunicacion = false;
                }
            }
            catch (Exception exec)
            {
                AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|Excepcion En Verificacion estado en recibo combustible|" + exec.Message);

            }
        }


        private void VerificarConexionRS232()
        {
            try
            {
                Boolean EsInicializado = false;

                if (this.EsTCPIP == false)
                {

                    if (PuertoVeederRoot == null)
                    {

                        while (!EsInicializado)
                        {
                            try
                            {

                                PuertoVeederRoot = new SerialPort();

                                PuertoVeederRoot.PortName = Puerto;
                                PuertoVeederRoot.BaudRate = 9600;
                                PuertoVeederRoot.DataBits = 8;
                                PuertoVeederRoot.StopBits = StopBits.One;
                                PuertoVeederRoot.Parity = Parity.None;
                                PuertoVeederRoot.ReadBufferSize = 4096;
                                PuertoVeederRoot.WriteBufferSize = 4096;

                                //Abre el puerto COM de comunicación con Veeder Root
                                PuertoVeederRoot.Open();

                                if (PuertoVeederRoot.IsOpen == true)
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
                        if (PuertoVeederRoot.IsOpen == false)
                        {
                            while (!EsInicializado)
                            {
                                try
                                {

                                    PuertoVeederRoot = new SerialPort();

                                    PuertoVeederRoot.PortName = Puerto;
                                    PuertoVeederRoot.BaudRate = 9600;
                                    PuertoVeederRoot.DataBits = 8;
                                    PuertoVeederRoot.StopBits = StopBits.One;
                                    PuertoVeederRoot.Parity = Parity.None;
                                    PuertoVeederRoot.ReadBufferSize = 4096;
                                    PuertoVeederRoot.WriteBufferSize = 4096;

                                    //Abre el puerto COM de comunicación con Veeder Root
                                    PuertoVeederRoot.Open();

                                    if (PuertoVeederRoot.IsOpen == true)
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
                AlmacenarEnArchivo(DateTime.Now + "|Conexion Veeder Serial Excepcion|" + exec.Message);

            }
        }


        private void VerificarConexionReciboCombustibleRS232(ref bool ExisteComunicacion)
        {
            int i = 0;
            try
            {
                Boolean EsInicializado = false;


                if (this.EsTCPIP == false)
                {

                    if (PuertoVeederRoot == null)
                    {

                        while (i <= 10 || EsInicializado == false)
                        {
                            try
                            {

                                PuertoVeederRoot = new SerialPort();
                                PuertoVeederRoot.PortName = Puerto;
                                PuertoVeederRoot.BaudRate = 9600;
                                PuertoVeederRoot.DataBits = 8;
                                PuertoVeederRoot.StopBits = StopBits.One;
                                PuertoVeederRoot.Parity = Parity.None;
                                PuertoVeederRoot.ReadBufferSize = 4096;
                                PuertoVeederRoot.WriteBufferSize = 4096;

                                //Abre el puerto COM de comunicación con Veeder Root
                                PuertoVeederRoot.Open();

                                if (PuertoVeederRoot.IsOpen == true)
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
                                AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|RS232: Error logeado:" + ex.Message + "-- ExisteComunicacion=" + ExisteComunicacion.ToString());

                                Thread.Sleep(500);
                            }
                        }

                    }
                    else
                    {
                        i = 0;
                        EsInicializado = false;
                        if (PuertoVeederRoot.IsOpen == false)
                        {


                            while (i <= 15 || EsInicializado == false)
                            {
                                try
                                {
                                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|RS232: Intento de apertura numero NO NULL :-- ExisteComunicacion=" + ExisteComunicacion.ToString());



                                    PuertoVeederRoot = new SerialPort();

                                    PuertoVeederRoot.PortName = Puerto;
                                    PuertoVeederRoot.BaudRate = 9600;
                                    PuertoVeederRoot.DataBits = 8;
                                    PuertoVeederRoot.StopBits = StopBits.One;
                                    PuertoVeederRoot.Parity = Parity.None;
                                    PuertoVeederRoot.ReadBufferSize = 4096;
                                    PuertoVeederRoot.WriteBufferSize = 4096;

                                    //Abre el puerto COM de comunicación con Veeder Root
                                    PuertoVeederRoot.Open();

                                    if (PuertoVeederRoot.IsOpen == true)
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
                                    AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|RS232: Error logeado NO NULL:" + ex.Message + "-- ExisteComunicacion=" + ExisteComunicacion.ToString());

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
                            PollingTimer.Stop();

                            if (ProcesoValidacionComunicacionVeederRoot())
                            {
                                ExisteComunicacion = true;
                                Thread.Sleep(5000);
                            }
                            else
                            {
                                ExisteComunicacion = false;
                            }
                            ConfigurarTimer();
                            return;
                            //PollingTimer.Start();

                        }
                        else
                        {
                            goto Validar;

                        }

                    }
                    else
                    {
                        ExisteComunicacion = false;
                        AlmacenarEnArchivo(DateTime.Now + "|Conexion|" + Comando + "|R232:NO Hay comunicacion con dispositivo veeder root - Validacion previa  al ajuste de tanques en turno o al recibo de combustible");

                    }

                }

            }
            catch (Exception exec)
            {
                AlmacenarEnArchivo(DateTime.Now + "|Conexion Veeder Serial Excepcion Recibo Combustible|" + exec.Message);

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
                AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Recibe peticion para reportar Inventario Petición MR");




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
                    ProcesoObtencionReporteVariables();

                    //Una vez terminada la encuesta, reinicia el Timer
                    ConfigurarTimer();
                    //PollingTimer.Start();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanques: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

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
                    ArchivoTramas = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Tramas" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(ArchivoRegistros);
                if (FileInf.Length > 30000000)
                {
                    SWRegistros.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistros = Application.StartupPath + "/LogueoVeederRoot/" + "VeederRoot-Sucesos" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
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
                AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Recibe peticion para reportar Inventario Petición Cierre Turno");



                int i = 0;
                //Valido si hay una encuenta en curso y espero a que finalize
                while (EncuestaEnProceso && i <= 10)//JDT - se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Esperando Finalizacion de Encuesta Periodica");

                    i++;
                    Thread.Sleep(200);

                }

                if (i >= 9)
                {
                    EncuestaEnProceso = false;
                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Finaliza espera periodica EncuestaEnProceso: " + EncuestaEnProceso.ToString());

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
                    ProcesoObtencionReporteVariables();

                    //Una vez terminada la encuesta, reinicia el Timer
                    //PollingTimer.Start();
                    ConfigurarTimer();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanquesCierreTurno: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }

        //RECIBE ENVENTO PARA OBTENER DATOS PARA REPORTAR INVENTARIO POR CIERRE DE TURNO DEL SERVICIO WINDOWS PARA VALIDACIONES DE AJUSTE DE TANQUES
        private void oEventos_InformarStocksTanquesCierreTurnoServicio(ref int idTurno)
        {
            try
            {
                AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Recibe peticion para reportar Inventario Petición Cierre Turno");

                int i = 0;
                //Valido si hay una encuenta en curso y espero a que finalize
                while (EncuestaEnProceso && i <= 10)//JDT - se valida la espera antes que finalice el timer debido a que se quedaba en una espera infinita
                {
                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Esperando Finalizacion de Encuesta Periodica");

                    i++;
                    Thread.Sleep(200);

                }

                if (i >= 9)
                {
                    EncuestaEnProceso = false;
                    AlmacenarEnArchivo(DateTime.Now + "|Evento|" + Comando + "|Finaliza espera periodica EncuestaEnProceso: " + EncuestaEnProceso.ToString());

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
                    ProcesoObtencionReporteVariables(true);

                    //Una vez terminada la encuesta, reinicia el Timer
                    ConfigurarTimer();
                    //PollingTimer.Start();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_InformarStocksTanquesCierreTurno: " + Excepcion;
                AlmacenarEnArchivo(DateTime.Now + "|Excepcion|" + Comando + "|" + MensajeExcepcion);

            }
        }

        #endregion
    }
}
