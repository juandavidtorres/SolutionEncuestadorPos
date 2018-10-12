using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using POSstation.Protocolos;
using System.Net.Sockets;
using System.Net;

namespace POSstation.Protocolos
{
    public class Develco:iProtocolo

    {
        #region EventoDeProtocolo

        private bool AplicaWindows = false;
        private bool AplicaTramas = true;
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

        //VARIABLES DE CONTROL
        /*Variables que se declaran como globales por la necesidad de compartirse entre diferentes funciones*/
        ComandosSurtidor ComandoEnviado;        //Almacena el COMANDO enviado al ultimo SURTIDOR encuestado
        bool ComandoAceptado;                   //Determina si el Surtidor acepto el comando enviado por el Host
        bool CondicionCiclo = true;             //Variable para garantizar el ciclo infinito
        bool FalloComunicacion;                 //Establece si hubo error en la comunicación (Surtidor no contesta)        
        bool TomarParcialTotalizador = false;   //Establece si debe tomar un parcial de totalizador para confirmar venta en 0
        byte StatusFinCarga;                    //Reporta los Mensajes de Fin de Despacho
        bool TecladoConectado;                  //Determina si el Teclado esta conectado en el Surtidor        
        byte TimeOut;                           //Determina el tiempo en que una Cara va estar desautorizada                
        byte CaraImpar;                           //CARA que se esta ENCUESTANDO
        byte CaraPar;
        decimal PrecioEDS;				        //Almacena el PRECIO vigente en la EDS, grabado en la Base de Datos
        decimal DensidadEDS;
        int BytesEsperados;                     //Declara la cantidad de bytes esperados (depende del comando transmitido)
        int CodigoSurtidor;                     //Surtidor que esta siendo encuestado        
        int TiempoEspera;                       //Tiempo de espera de respuesta del surtidor
        string PuertoSurtidores;

        //ENUMERACIONES UTILIZADA PARA CREAR VARIABLES        
        private enum ComandosSurtidor		    //Define los COMANDOS que se envian al Surtidor
        {
            //Mensajes de Pedido de Informacion
            PedidoConfiguracion = 0x00,
            PedidoDespacho = 0x01,
            PedidoStatus = 0x02,
            PedidoTicket = 0x0F,
            PedidoStatusComunicacion = 0x10,
            PedidoVariablesInternas = 0x12,
            PedidoTarjeta = 0x15,
            PedidoTotales = 0x16,
            PedidoStatusExtendido = 0x17,
            //Mensajes de Seteado
            ResetParciales1 = 0x03,
            ResetPariacles2 = 0x04,
            SetTotales1 = 0x05,
            SetTotales2 = 0x06,
            SetDensidad = 0x07,
            SetPrecio = 0x08,
            SetNumeroSurtidor = 0x09,
            SetHora = 0x0A,
            SetFecha = 0x0B,
            SetReset = 0x0E,
            SetStatusComunicacion = 0x11,
            SetRango = 0x13,
            SetCliente = 0x20,
            //Mensajes de Control
            Habilitacion = 0x0C,
            LimiteImporte = 0x0D,
            Desautorizar = 0x18
        }

        //ARREGLOS SIMPLES DE INFORMACION NECESARIA POR CARA
        /*Tramas compuestas de bytes para comunicacion con SURTIDOR */
        byte[] TramaRx = new byte[1];           //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];           //Almacena la TRAMA A ENVIAR

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        //SharedEventsFuelStation.CMensaje oEvento;    //Controla la comunicacion entre las aplicaciones por medio de eventos
        SerialPort PuertoCom = new SerialPort();
        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        //System.Timers.Timer PollingTimer = new System.Timers.Timer(10); //Definicion del TIMER DE ENCUESTA



        //Diccionario donde se almacenan las Caras y sus propiedades
        Dictionary<byte, RedSurtidor> PropiedadesCara;

        //Instancia Arreglo de lecturas para reportar reactivación de cara
        System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

        //VARIABLES VARIAS
        string ArchivoRegistroSucesos; //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        StreamWriter SWRegistro;

        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        StreamWriter SWTramas; //Variable utilizada para escribir en el archivo
        AsyncCallback callBack = new AsyncCallback(CallBackMethod);

        #endregion

        #region METODOS PRINCIPALES

        //PUNTO DE ARRANQUE DE LA CLASE        
        //public Develco(string Puerto, byte NumerodeCaras, byte CaraInicial, string strPrecioEDS, List<Cara> ListaPropiedadesCara)

        //TCPIP
        bool EsTCPIP;
        string DireccionIP;
        string Puerto;
        int Bytes_leidos;
        TcpClient ClienteDevelco;


        NetworkStream Stream;

        //byte[] TramaRxTemporal = new byte[250];
        int BytesRecibidos = 0;


        public Develco(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {


                this.Puerto = Puerto; 

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar inconsistencias en el proceso logico
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Sucesos.(" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);


                //Escribe encabezado en archivo de Estados
                SWRegistro.WriteLine("===================|====|==|======|==================================");
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2010.07.23-1000");
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2012.02.17- 0840"); // Para controlar que la venta si se realizó. DCf -- 17/02/2012
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2013.11.28- 1030");//Environment.CurrentDirectory  por  Application.StartupPath 
                SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo Develco modificado_IP 2018.03.08- 1638");//DCF Archivos .txt 08/03/2018  
                SWRegistro.Flush();

                ////Instancia los eventos disparados por la aplicación cliente
                //oEvento = OEventoAutorizador;
                //oEvento.CambioPrecio += oEvento_CambioPrecio;
                //oEvento.VentaAutorizada += oEvento_VentaAutorizada;
                //oEvento.TurnoAbierto +=  oEvento_TurnoAbierto;
                //oEvento.TurnoCerrado += oEvento_TurnoCerrado;
                //oEvento.InactivarCaraCambioTarjeta += oEvento_InactivarCaraCambioTarjeta;
                //oEvento.FinalizarCambioTarjeta += oEvento_FinalizarCambioTarjeta;
                //oEvento.CambiarDensidad +=oEvento_CambiarDensidad;
                //oEvento.FinalizarVentaPorMonitoreoCHIP +=oEvento_FinalizarVentaPorMonitoreoCHIP;
                //oEvento.CerrarProtocolo += oEvento_CerrarProtocolo;


                //Almacena el Precio de Venta establecido para la EDS
                //PrecioEDS = Convert.ToDecimal(strPrecioEDS);

                PuertoSurtidores = Puerto;

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                PuertoCom.PortName = Puerto;
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.BaudRate = 4800;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 1024;
                    PuertoCom.WriteBufferSize = 1024;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }
                //Armar diccionario
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica                
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|Develco: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public Develco(bool EsTCPIP, string DireccionIP, string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                //Almacena en variables globales los parámetros de comunicación
                this.EsTCPIP = EsTCPIP; //DCF 08/07/2016
                this.DireccionIP = DireccionIP; //DCF 08/07/2016
                this.Puerto = Puerto; //DCF 08/07/2016


                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar inconsistencias en el proceso logico
                ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Sucesos.(" + Puerto + ").txt";
                SWRegistro = File.AppendText(ArchivoRegistroSucesos);

                ////Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);


                //Escribe encabezado en archivo de Estados
                SWRegistro.WriteLine("===================|====|==|======|==================================");
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2010.07.23-1000");
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2012.02.17- 0840"); // Para controlar que la venta si se realizó. DCf -- 17/02/2012
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado 2013.11.28- 1030");//Environment.CurrentDirectory  por  Application.StartupPath 
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo modificado_IP 2016.05.04- 0841");//ETH_RS485 Interface DCF
                //SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo Develco modificado_IP 2016.07.08- 1140");//DCF 08/07/2016
                SWRegistro.WriteLine(DateTime.Now + "|Cara|0|Inicio|Protocolo Develco modificado_IP 2018.03.08- 1638");//DCF Archivos .txt 08/03/2018  

                SWRegistro.Flush();


                PuertoSurtidores = Puerto;

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                PuertoCom.PortName = Puerto;

                //EsTCPIP = EsTCPIP_;

                if (EsTCPIP)
                {
                    try
                    {
                        //Crea y abre la conexión con el Servidor
                        ClienteDevelco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                        Stream = ClienteDevelco.GetStream();

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
                    PuertoCom.BaudRate = 4800;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Even;
                    PuertoCom.ReadBufferSize = 1024;
                    PuertoCom.WriteBufferSize = 1024;
                    try
                    {
                        PuertoCom.Open();
                    }
                    catch (Exception ex)
                    {
                        throw ex; //throw new Exception ("Comunicacion con surtidor no disponible");
                    }
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();
                }
                //Armar diccionario
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica                
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|Develco: " + Excepcion);
                SWRegistro.Flush();
            }
        }


        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        private void CicloCara()
        {
            try
            {

                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //para loguear los factores
                foreach (RedSurtidor ORedCaras2 in PropiedadesCara.Values)
                {
                    byte CaraEncuestada2 = ORedCaras2.Cara;

                    //if (EstructuraRedSurtidor[CaraTmp].MultiplicadorPrecioVenta == 0)-- 09/05/2012
                    if (PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta == 0)
                    {
                        PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta = 1;
                    }

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada2
                           + "|FactorVolumen: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorVolumen)
                           + " - FactorTotalizador: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorTotalizador)
                           + " - FactorImporte: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorImporte)
                           + " - FactorPrecio: " + Math.Log10(PropiedadesCara[CaraEncuestada2].FactorPrecio)
                           + " - MultiplicadorPrecioVenta: " + PropiedadesCara[CaraEncuestada2].MultiplicadorPrecioVenta);

                    SWRegistro.Flush();
                }




                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    VerifySizeFile();

                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
                    {
                        //Si la cara está activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {
                            //Se define el IdSurtidor a encuestarse
                            if (ORedCaras.Cara % 2 != 0)
                            {
                                //Se asigna el IdSurtidor a encuestarse
                                CodigoSurtidor = ORedCaras.IdSurtidor;

                                //Se asigna la cara para manejo del Objeto Diccionario de Caras
                                CaraImpar = (byte)(CodigoSurtidor * 2 - 1);
                                CaraPar = (byte)(CodigoSurtidor * 2);

                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandosSurtidor.PedidoStatus))
                                    TomarAccion();
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Comando PedidoStatus Fallido en Ciclo");
                                    SWRegistro.Flush();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|CicloCara: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandosSurtidor ComandoaEnviar)
        {
            try
            {
                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma Trama a ser enviada
                ArmarTramaTx(ComandoaEnviar);

                //2 reintentos de envio de comando recomendados por Gilbarco
                do
                {
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
                } while (FalloComunicacion == true && Reintentos <= 3);

                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion == true)
                {
                    //ACTIVACIÓN/INACTIVACIÓN Cara Par e Impar
                    //Si la cara impar se va a Inactivar
                    if (PropiedadesCara[CaraImpar].InactivarCara == true)
                    {
                        PropiedadesCara[CaraImpar].InactivarCara = false;
                        PropiedadesCara[CaraImpar].Activa = false;
                        string Puerto = PropiedadesCara[CaraImpar].PuertoParaImprimir;
                        IniciarCambioTarjeta( CaraImpar,  Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Evento|Informa Inactivacion en Fallo de Comunicacion");
                        SWRegistro.Flush();
                    }
                    //Si la cara impar se va a activar
                    if (PropiedadesCara[CaraImpar].ActivarCara == true)
                    {
                        PropiedadesCara[CaraImpar].Activa = false;
                        string Mensaje = "No se puede ejecutar activacion: Cara " + CaraImpar + " con fallo de comunicacion";
                        bool Imprime = true;
                        bool Terminal = false;
                        string Puerto = PropiedadesCara[CaraImpar].PuertoParaImprimir;
                    
                        ExcepcionOcurrida(Mensaje, Imprime, Terminal, Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Error|" + Mensaje);
                        SWRegistro.Flush();
                    }

                    //Si la cara par se va a Inactivar
                    if (PropiedadesCara[CaraPar].InactivarCara == true)
                    {
                        PropiedadesCara[CaraPar].InactivarCara = false;
                        PropiedadesCara[CaraPar].Activa = false;
                        string Puerto = PropiedadesCara[CaraPar].PuertoParaImprimir;
                        IniciarCambioTarjeta( CaraPar,  Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Evento|Informa Inactivacion en Fallo de Comunicacion");
                        SWRegistro.Flush();
                    }
                    //Si la cara par se va a activar
                    if (PropiedadesCara[CaraPar].ActivarCara == true)
                    {
                        PropiedadesCara[CaraPar].Activa = false;
                        string Mensaje = "No se puede ejecutar activacion: Cara " + CaraPar + " con fallo de comunicacion";
                        bool Imprime = true;
                        bool Terminal = false;
                        string Puerto = PropiedadesCara[CaraPar].PuertoParaImprimir;
                        ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  Puerto);
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Error|" + Mensaje);
                        SWRegistro.Flush();
                    }

                    //CONTROL FALLO LECTURA Cara Par e Impar
                    //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                    if (!PropiedadesCara[CaraImpar].FalloTomaLecturaTurno)
                    {
                        string MensajeErrorLectura = "Error en comunicacion con Surtidor";
                        if (PropiedadesCara[CaraImpar].TomarLecturaAperturaTurno)
                        {
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraImpar].TomarLecturaAperturaTurno = false;
                            PropiedadesCara[CaraPar].TomarLecturaAperturaTurno = false;
                            CancelarProcesarTurno( CaraImpar,  MensajeErrorLectura,  EstadoTurno);
                            CancelarProcesarTurno( CaraPar,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Evento|Reporta Cancelacion de Inicio de Turno: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraImpar].TomarLecturaCierreTurno)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraImpar].TomarLecturaCierreTurno = false;
                            PropiedadesCara[CaraPar].TomarLecturaCierreTurno = false;
                            CancelarProcesarTurno( CaraImpar,  MensajeErrorLectura,  EstadoTurno);
                            CancelarProcesarTurno( CaraPar,  MensajeErrorLectura,  EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Evento|Reporta Cancelacion de Fin de Turno: " + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        //Se establece valor de la variable para que indique que ya fue reportado el error
                        PropiedadesCara[CaraImpar].FalloTomaLecturaTurno = true;
                        PropiedadesCara[CaraPar].FalloTomaLecturaTurno = true;
                    }

                    //Ingresa a este condicional si el surtidor NO responde y si no se ha logueado aún la falla
                    if (!PropiedadesCara[CaraImpar].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Perdida de comunicacion. Estado cara " + CaraImpar +
                            ": " + PropiedadesCara[CaraImpar].Estado + " y estado cara " + CaraPar + ": " + PropiedadesCara[CaraPar].Estado +
                            " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraImpar].FalloReportado = true;
                        PropiedadesCara[CaraPar].FalloReportado = true;
                    }
                }
                else
                {
                    if (PropiedadesCara[CaraImpar].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Reestablecimiento de comunicacion. Estado cara " +
                            CaraImpar + ": " + PropiedadesCara[CaraImpar].Estado + " y Estado cara " +
                            CaraPar + ": " + PropiedadesCara[CaraPar].Estado + " - Comando enviado: " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraImpar].FalloReportado = false;
                        PropiedadesCara[CaraPar].FalloReportado = false;
                    }
                }
                //Esta funcion retorna 
                return !FalloComunicacion;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ProcesoEnvioComando: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA
        private void ArmarTramaTx(ComandosSurtidor ComandoTx)
        {
            try
            {
                //Asigna a la cara a encustar el comando que fue enviado
                ComandoEnviado = ComandoTx;

                /* Configuracion por defecto de los valores de TimeOut, Trama y Bytes Esperados (Aplica completamente a los
                 * Comandos Estado y Enviar Datos */
                TramaTx = new byte[5];
                BytesEsperados = 6;

                //Termina de armar Trama de Envio dependiendo del comando
                switch (ComandoTx)
                {
                    case ComandosSurtidor.PedidoConfiguracion:
                        //Estipula la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 67;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 500;
                        break;
                    case ComandosSurtidor.PedidoDespacho:
                        //Estipula la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 27;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 1000;//Antes 400
                        break;
                    case ComandosSurtidor.PedidoStatus:
                        //Estipula la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 10;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 800;//Antes 350
                        break;
                    case ComandosSurtidor.PedidoTicket:
                        //Crea vector con la longitud total de la trama a transmitir
                        TramaTx = new byte[6];
                        //Estipula la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 24;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 600;//Antes 400

                        //Evalúa si el fin de venta a pedir es el de la LÍNEA IMPAR                        
                        if (PropiedadesCara[CaraImpar].EsVentaParcial)
                        {
                            TramaTx[4] = 0;
                            break;
                        }

                        //Evalúa si el fin de venta a pedir es el de la LÍNEA PAR
                        else if (PropiedadesCara[CaraPar].EsVentaParcial)
                        {
                            TramaTx[4] = 1;
                            break;
                        }
                        break;
                    case ComandosSurtidor.SetPrecio:
                        //Crea vector con la longitud total de la trama a transmitir
                        TramaTx = new byte[9];

                        //DCF strPrecio para bolivia 17/03/2010
                         string strPrecio =
                         Convert.ToInt32((PropiedadesCara[CaraImpar].ListaGrados[0].PrecioNivel1 * PropiedadesCara[CaraImpar].FactorPrecio)).ToString().PadLeft(4, '0'); //18-05-2011 DCF
                      
                        //Wal esto no funciona con precio que tiene cifras decimales.
                         //strPrecio =
                         //   Convert.ToString(PropiedadesCara[CaraImpar].ListaGrados[0].PrecioNivel1 *
                         //   PropiedadesCara[CaraImpar].FactorPrecio).PadLeft(4, '0');
                        for (int i = 4; i <= 7; i++)
                            TramaTx[i] = Convert.ToByte(Convert.ToChar(strPrecio.Substring(i - 4, 1)));
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 900;
                        break;
                    case ComandosSurtidor.PedidoTotales:
                        //Crea vector con la longitud total de la trama a transmitir
                        TramaTx = new byte[6];
                        //Almacena la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 22;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 800;//Antes 500

                        //Evalúa si se requiere tomar el Totalizador de la LÍNEA IMPAR
                        if (PropiedadesCara[CaraImpar].TomarLectura)
                        {
                            TramaTx[4] = 0;
                            break;
                        }

                        //Evalúa si se requiere tomar el Totalizador de la LÍNEA PAR
                        else if (PropiedadesCara[CaraPar].TomarLectura)
                        {
                            TramaTx[4] = 1;
                            break;
                        }

                        //Evalúa si debe tomar un Parcial del totalizador
                        if (TomarParcialTotalizador)
                            TramaTx[4] = Convert.ToByte(TramaTx[4] | 0x02);
                        break;
                    case ComandosSurtidor.Desautorizar:
                        //Crea vector con la longitud total de la trama a transmitir
                        TramaTx = new byte[7];
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 400;
                        //Se envia comando de desautorizacion a la Cara IMPAR
                        if (PropiedadesCara[CaraImpar].DesautorizarDespacho &&
                            !PropiedadesCara[CaraPar].DesautorizarDespacho)
                            TramaTx[4] = 0;

                        //Se envia comando de desautorizacion a la Cara PAR
                        else if (!PropiedadesCara[CaraImpar].DesautorizarDespacho &&
                            PropiedadesCara[CaraPar].DesautorizarDespacho)
                            TramaTx[4] = 1;

                        //Se asigna el valor de tiempo de Desautorizacion
                        TramaTx[5] = Convert.ToByte(TimeOut);
                        break;
                    case ComandosSurtidor.PedidoStatusExtendido:
                        //Almacena la cantidad de bytes esperados en la trama Respuesta
                        BytesEsperados = 26;
                        //Estipula la cantidad de tiempo en ms, que el Host debe esperar hasta recibir respuesta
                        TiempoEspera = 500;//Antes 300
                        break;
                }

                //Asigna el codigo del comando al encabezado de la trama
                TramaTx[0] = Convert.ToByte(ComandoEnviado);

                //Almacena el numero del surtidor en formato ASCII en el trama a transmitir
                string strSurtidor = Convert.ToString(CodigoSurtidor).PadLeft(3, '0');
                for (int i = 1; i <= 3; i++)
                    TramaTx[i] = Convert.ToByte(Convert.ToChar(strSurtidor.Substring(i - 1, 1)));

                //Calcula el Checksum y lo inserta en el bytes correspondiente de la trama
                TramaTx[TramaTx.Length - 1] = CalcularChecksum(TramaTx);
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ENVIA EL COMANDO AL SURTIDOR
        private void EnviarComando()
        {
            try
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

                SWTramas.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                    "|" + CodigoSurtidor + "|Tx|" + ComandoEnviado + "|" + strTrama);

                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TiempoEspera);

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|EnviarComando: " + Excepcion);
                SWRegistro.Flush();
            }
        }



        private void EnviarComando_TCPIP()
        {
           try
           {

             try
              {
                ////Limpia todo lo que este en el Buffer de salida y Buffer de entrada del puerto
                //PuertoCom.DiscardOutBuffer();
                //PuertoCom.DiscardInBuffer();

                ////Escribe en el puerto el comando a Enviar.
                //PuertoCom.Write(TramaTx, 0, TramaTx.Length);

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
                    "|" + CodigoSurtidor + "|Tx|" + ComandoEnviado + "|" + strTrama);

                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(TiempoEspera);

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|EnviarComando: " + Excepcion);
                SWRegistro.Flush();
            }
        }


        public void RecibirInformacion_TCPIP()
        {
            try
            {
               

                if (Stream == null)
                {
                    FalloComunicacion = true;
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
                        FalloComunicacion = false;


                        //Definicion de Trama Temporal
                        byte[] TramaTemporal = new byte[Bytes_leidos];

                        ////Almacena informacion en la Trama Temporal para luego eliminarle el eco
                        // PuertoCom.Read(TramaTemporal, 0, Bytes_leidos);
                        //PuertoCom.DiscardInBuffer();

                        //Se dimensiona la Trama a evaluarse (TramaRx)
                        TramaRx = new byte[TramaTemporal.Length ];

                        //Almacena los datos reales (sin eco) en TramaRx
                        for (int i = 0; i <= (TramaTemporal.Length  - 1); i++)
                            TramaRx[i] = TramaRxTemporal[i];


                        ///////////////////////////////////////////////////////////////////////////////
                        //LOGUEO DE TRAMA RECIBIDA
                        string strTrama = "";
                        for (int i = 0; i <= TramaRx.Length - 1; i++)
                            strTrama += TramaRx[i].ToString("X2") + "|";


                       
                            SWTramas.WriteLine(
                                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                "|" + CodigoSurtidor + "|Rx|" + strTrama);

                            SWTramas.Flush();

                    

                    /////////////////////////////////////////////////////////////////////////////////
                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    if (Bytes_leidos == BytesEsperados) // que se lea siempre la respuesta del surtidor  al final se analiza y se indica si existe error 
                    {
                        AnalizarTrama();

                    }

                    //DCF Modificacion 14/08/2015 EDS Sodis Aeropuerto  Leer siempre la respuesta del surtidor 
                    else if (FalloComunicacion == false)
                    {

                        //SWRegistro.WriteLine(DateTime.Now + "|Error|" + " Bytes_leidos = " + Bytes_leidos + " | BytesEsperados = |" + BytesEsperados);
                        //SWRegistro.Flush();

                        FalloComunicacion = true;
                    }

                    //SWRegistro.Flush();
                }
                else if (FalloComunicacion == false)
                {

                    FalloComunicacion = true;

                }


            }
            catch (Exception Excepcion)
            {
                LimpiarSockets();//Borro de memoria el cliente TCP-IP ''Juan David Torres
                string MensajeExcepcion = "Excepcion en el Metodo RecibirInformacion: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CodigoSurtidor + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;
                TramaRx = new byte[Bytes];

                if (Bytes > 0)
                {
                    PuertoCom.Read(TramaRx, 0, Bytes);
                    PuertoCom.DiscardInBuffer();
                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA TRANSMITIDA
                    string strTrama = "";
                    for (int i = 0; i <= TramaRx.Length - 1; i++)
                        strTrama += TramaRx[i].ToString("X2") + "|";

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                        "|" + CodigoSurtidor + "|Rx|" + ComandoEnviado + "|" + strTrama);

                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////
                }

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes == BytesEsperados)
                {
                    PropiedadesCara[CaraImpar].FalloReportado = false;
                    PropiedadesCara[CaraPar].FalloReportado = false;

                    //Comprueba integridad y correspondencia de la trama de respuesta
                    ComprobacionTramaRx();
                    //Si no hubo error de integridad en la trama, se realiza su respectivo analisis
                    if (FalloComunicacion == false)
                        AnalizarTrama();
                }
                else //if (FalloComunicacion == false)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|" + ComandoEnviado +
                        ": Bytes esperados: " + BytesEsperados + " - Bytes recibidos: " + Bytes);
                    SWRegistro.Flush();
                    FalloComunicacion = true;
                }

                SWRegistro.Flush();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecibirInformacion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {
            try
            {
                //Dependiendo del comando enviado, realiza el analisis de la trama recibida
                switch (ComandoEnviado)
                {
                    case ComandosSurtidor.PedidoConfiguracion:
                        RecuperarConfiguracion();
                        break;

                    case ComandosSurtidor.PedidoDespacho:
                        RecuperarParcialesdeVenta();
                        break;

                    case ComandosSurtidor.PedidoTicket:
                        RecuperarDatosFindeVenta();
                        break;

                    case ComandosSurtidor.PedidoStatus:
                        AsignarEstado();
                        break;

                    case ComandosSurtidor.PedidoStatusExtendido:
                        RecuperarStatusExtendido();
                        break;

                    case ComandosSurtidor.PedidoTotales:
                        RecuperarTotalizadores();
                        break;

                    case ComandosSurtidor.SetRango:
                    case ComandosSurtidor.SetPrecio:
                    case ComandosSurtidor.LimiteImporte:
                    case ComandosSurtidor.Desautorizar:
                        RecuperarConfirmacion();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|AnalizarTrama: " + Excepcion);
                SWRegistro.Flush();
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
                if (ClienteDevelco == null)
                {
                    Boolean EsInicializado = false;
                    SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CodigoSurtidor + "|*7|Verificando conexion 1 " + EsInicializado);

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
        "|" + CodigoSurtidor + "|*8|Verificando conexion 2 " + EsInicializado);

                            SWTramas.Flush();
                            ClienteDevelco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                            SWTramas.WriteLine(
                 DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                 DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 "|" + CodigoSurtidor + "|*9|Verificando conexion 3" + EsInicializado);

                            SWTramas.Flush();

                            if (ClienteDevelco == null)
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

                        if (ClienteDevelco != null)
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
                 "|" + CodigoSurtidor + "|*9|Verificando conexio 4");

                    SWTramas.Flush();
                }

                Boolean estadoAnterior = true;
                if (!this.ClienteDevelco.Client.Connected)
                {
                    estadoAnterior = false;
                    SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - BeginDisconnect");
                    SWRegistro.Flush();

                    try
                    {
                        ClienteDevelco.Client.BeginDisconnect(true, callBack, ClienteDevelco);

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



                while (!this.ClienteDevelco.Client.Connected)
                {
                    try
                    {
                        iReintento = iReintento + 1;
                        SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + Comando + "|Perdida de comunicacion - Intento Reconexion: " + iReintento.ToString());
                        SWRegistro.Flush();


                        ClienteDevelco.Client.BeginConnect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto), callBack, ClienteDevelco);
                        //ClienteGilbarco.Client.Connect(Dns.GetHostAddresses(this.DireccionIP), Convert.ToInt16(this.Puerto));

                        if (!this.ClienteDevelco.Client.Connected)
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
                this.Stream = ClienteDevelco.GetStream();
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

        void AbrirSocketReintento()
        {
            try
            {
                Thread.Sleep(20);
                LimpiarVariableSocket();//Libero los recursos antes de iniciar una nueva conexcion con la veeder
                ClienteDevelco = new TcpClient(DireccionIP, Convert.ToInt16(Puerto));
                Stream = ClienteDevelco.GetStream();
                if (this.ClienteDevelco.Client.Connected == true)
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
            catch (Exception ex)//DCF 08/07/2016 
            {
                SWRegistro.WriteLine(DateTime.Now + "|Conexion|" + "|Falla AbrirSocketReintento  Creando Socket : " + "-DireccionIP: " + DireccionIP + " -Puerto: " + Puerto +" / " + ex.Message);
                SWRegistro.Flush();

            }

        }

        void LimpiarVariableSocket()
        {
            try
            {
                ClienteDevelco.Close();
                Stream.Close();
                Stream.Dispose();
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Metodo|" + "LimpiarVariableSocket: " + "Mensaje: " + ex.Message);
                SWRegistro.Flush();
            }

        }

        public void LimpiarSockets()
        {
            try
            {
                //ClienteGilbarco.Client.Disconnect(false);  
                ClienteDevelco.Client.Close();
                ClienteDevelco.Close();
                Stream.Close();
                Stream.Dispose();
                Stream = null;
                ClienteDevelco = null;
            }
            catch (Exception ex)
            {
                SWRegistro.WriteLine(DateTime.Now + "|LimpiarSockets:" + ex.Message);
                SWRegistro.Flush();

            }

        }



        public void VerifySizeFile()//Logueo
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }



                //FileInfo 
                FileInf = new FileInfo(ArchivoRegistroSucesos);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    ArchivoRegistroSucesos = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMddHHmm") + "-Develco-Sucesos.(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(ArchivoRegistroSucesos);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CodigoSurtidor + "|Excepcion|VerifySizeFile: " + Excepcion);
                SWRegistro.Flush();
            }

        }

        //RECUPERA DATOS DE TOTALIZADORES Y PRECIO POR SURTIDOR
        private void RecuperarConfiguracion()
        {
            try
            {
                //Iniciliza Variables de lectura de Caras
                PropiedadesCara[CaraImpar].Lectura = 0;
                PropiedadesCara[CaraPar].Lectura = 0;

                //Inicializa Variables de Precio
                PropiedadesCara[CaraImpar].PrecioCara = 0;
                PropiedadesCara[CaraPar].PrecioCara = 0;

                //Totalizador Cara Impar
                PropiedadesCara[CaraImpar].Lectura = ObtenerValor(TramaRx, 10, 15);
                //Totalizador Cara Par
                PropiedadesCara[CaraPar].Lectura = ObtenerValor(TramaRx, 22, 27);

                //Precios
                PropiedadesCara[CaraImpar].PrecioCara = ObtenerValor(TramaRx, 56, 59);
                PropiedadesCara[CaraImpar].PrecioCara = PropiedadesCara[CaraPar].PrecioCara / PropiedadesCara[CaraPar].FactorPrecio;
                PropiedadesCara[CaraPar].PrecioCara = PropiedadesCara[CaraImpar].PrecioCara;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarConfiguracion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //RECUPERA DATOS DE PARCIALES DE VENTA DEL SURTIDOR
        private void RecuperarParcialesdeVenta()
        {
            try
            {
                //Valores de Importe y Volumen de Cara Impar
                PropiedadesCara[CaraImpar].TotalVenta = ObtenerValor(TramaRx, 4, 9) / PropiedadesCara[CaraPar].FactorImporte;
                PropiedadesCara[CaraImpar].Volumen = ObtenerValor(TramaRx, 10, 14) / PropiedadesCara[CaraPar].FactorVolumen;

                //Valores de Importes de Cara Par
                PropiedadesCara[CaraPar].TotalVenta = ObtenerValor(TramaRx, 15, 20) / PropiedadesCara[CaraPar].FactorImporte;
                PropiedadesCara[CaraPar].Volumen = ObtenerValor(TramaRx, 21, 25) / PropiedadesCara[CaraPar].FactorVolumen;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarParcialesdeVenta: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //RECUPERA LOS DATOS DEL FINAL DE LA VENTA
        private void RecuperarDatosFindeVenta()
        {
            try
            {
                decimal PrecioObtenido = ObtenerValor(TramaRx, 16, 19);

                //Recupera el Status de Fin de Despacho
                StatusFinCarga = TramaRx[22];

                //Si se requiere los valores de fin de venta de la cara Impar
                if (PropiedadesCara[CaraImpar].EsVentaParcial)
                {
                    //Precio Cara Impar
                    PropiedadesCara[CaraImpar].PrecioCara = PrecioObtenido / PropiedadesCara[CaraImpar].FactorPrecio;
                    //Importe Cara Impar
                    PropiedadesCara[CaraImpar].TotalVenta = ObtenerValor(TramaRx, 5, 10) / PropiedadesCara[CaraImpar].FactorImporte;
                    //Volumen Cara Impar
                    PropiedadesCara[CaraImpar].Volumen = ObtenerValor(TramaRx, 11, 15) / PropiedadesCara[CaraImpar].FactorVolumen;
                }
                //Si se requiere los valores de fin de venta de la cara Par
                else if (PropiedadesCara[CaraPar].EsVentaParcial)
                {
                    //Precio Cara Par
                    PropiedadesCara[CaraPar].PrecioCara = PrecioObtenido / PropiedadesCara[CaraPar].FactorPrecio;
                    //Importe Cara Par
                    PropiedadesCara[CaraPar].TotalVenta = ObtenerValor(TramaRx, 5, 10) / PropiedadesCara[CaraPar].FactorImporte;
                    //Volumen Cara Par
                    PropiedadesCara[CaraPar].Volumen = ObtenerValor(TramaRx, 11, 15) / PropiedadesCara[CaraPar].FactorVolumen;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarDatosFindeVenta: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ASIGNA ESTADO LUEGO DE RECIBIR LA RESPUESTA A UN COMANDO DE PEDIDO DE STATUS
        private void AsignarEstado()
        {
            try
            {
                //Determina el estado de carga de la Cara Impar
                if ((TramaRx[6] & 0x80) == 0x80)
                    PropiedadesCara[CaraImpar].EstadoDespacho = WorkStatus.NoEnCarga;
                else if ((TramaRx[6] & 0x40) == 0x40)
                    PropiedadesCara[CaraImpar].EstadoDespacho = WorkStatus.FinalDeCarga;
                else if ((TramaRx[6] & 0x20) == 0x20)
                    PropiedadesCara[CaraImpar].EstadoDespacho = WorkStatus.EnCarga;
                else if (TramaRx[6] == 0x00)
                    PropiedadesCara[CaraImpar].EstadoDespacho = WorkStatus.ComienzoCarga;

                //Determina el estado de carga de la Cara Par
                if ((TramaRx[7] & 0x80) == 0x80)
                    PropiedadesCara[CaraPar].EstadoDespacho = WorkStatus.NoEnCarga;
                else if ((TramaRx[7] & 0x40) == 0x40)
                    PropiedadesCara[CaraPar].EstadoDespacho = WorkStatus.FinalDeCarga;
                else if ((TramaRx[7] & 0x20) == 0x20)
                    PropiedadesCara[CaraPar].EstadoDespacho = WorkStatus.EnCarga;
                else if (TramaRx[7] == 0x00)
                    PropiedadesCara[CaraPar].EstadoDespacho = WorkStatus.ComienzoCarga;

                //Determina el Hardware Status del Surtidor
                switch (TramaRx[8] & 0x03)
                {
                    case 0x00:
                        PropiedadesCara[CaraImpar].EstadoHardware = HardwareStatus.Reposo;
                        PropiedadesCara[CaraPar].EstadoHardware = HardwareStatus.Reposo;
                        break;
                    case 0x01:
                        PropiedadesCara[CaraImpar].EstadoHardware = HardwareStatus.Extraida;
                        PropiedadesCara[CaraPar].EstadoHardware = HardwareStatus.Reposo;
                        break;
                    case 0x02:
                        PropiedadesCara[CaraImpar].EstadoHardware = HardwareStatus.Reposo;
                        PropiedadesCara[CaraPar].EstadoHardware = HardwareStatus.Extraida;
                        break;
                    case 0x03:
                        PropiedadesCara[CaraImpar].EstadoHardware = HardwareStatus.Extraida;
                        PropiedadesCara[CaraPar].EstadoHardware = HardwareStatus.Extraida;
                        break;
                }
                //Determina el Estado del pulsador de cada una de las Caras
                switch (TramaRx[8] & 0x0C)
                {
                    case 0x00:
                        PropiedadesCara[CaraImpar].Pulsador = StatusPulsador.Desactivado;
                        PropiedadesCara[CaraPar].Pulsador = StatusPulsador.Desactivado;
                        break;
                    case 0x04:
                        PropiedadesCara[CaraImpar].Pulsador = StatusPulsador.Activado;
                        PropiedadesCara[CaraPar].Pulsador = StatusPulsador.Desactivado;
                        break;
                    case 0x08:
                        PropiedadesCara[CaraImpar].Pulsador = StatusPulsador.Desactivado;
                        PropiedadesCara[CaraPar].Pulsador = StatusPulsador.Activado;
                        break;
                    case 0x0C:
                        PropiedadesCara[CaraImpar].Pulsador = StatusPulsador.Activado;
                        PropiedadesCara[CaraPar].Pulsador = StatusPulsador.Activado;
                        break;
                }

                //Asigna estado Consolidado de Ambas Caras
                ConsolidarEstado(CaraImpar);
                ConsolidarEstado(CaraPar);

                //Almacena codigo de Error de la Cara Impar
                if (PropiedadesCara[CaraImpar].ErrorCara != Convert.ToByte(TramaRx[6] & 0x1F))
                {
                    PropiedadesCara[CaraImpar].ErrorCara = Convert.ToByte(TramaRx[6] & 0x1F);
                    if (PropiedadesCara[CaraImpar].ErrorCara > 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Proceso|En estado de Error: " +
                            PropiedadesCara[CaraImpar].ErrorCara);
                        SWRegistro.Flush();
                    }
                }

                //Almacena codigo de Error de la Cara Par
                if (PropiedadesCara[CaraPar].ErrorCara != Convert.ToByte(TramaRx[7] & 0x1F))
                {
                    PropiedadesCara[CaraPar].ErrorCara = Convert.ToByte(TramaRx[7] & 0x1F);
                    if (PropiedadesCara[CaraPar].ErrorCara > 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Proceso|En estado de Error: " +
                            PropiedadesCara[CaraPar].ErrorCara);
                        SWRegistro.Flush();
                    }
                }

                //Establece si el teclado esta conectado
                if ((TramaRx[8] & 0x40) == 0x40)
                    TecladoConectado = true;
                else
                    TecladoConectado = false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|AsignarEstado: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CONSOLIDA ESTADO DE LA CARA TENIENDO EN CUENTA EL WORK STATUS Y EL HARDWARE STATUS
        private void ConsolidarEstado(byte CaraEncuestada)
        {
            try
            {
                //Almacena ultimo estado de la Cara
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                //Realiza la consolidacion del Hardware Status y Work Status de la cara
                switch (PropiedadesCara[CaraEncuestada].EstadoHardware)
                {
                    //Si la Manguera esta en reposo se establece como en espera
                    case HardwareStatus.Reposo:
                        if (!PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.DevelcoEspera)
                                PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoEspera;
                        }
                        else if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.DevelcoFinDespachoHardware)
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoFinDespachoHardware;
                        break;
                    //Si la Manguera esta extraida se evalua el estado de la cara con respecto al Despacho
                    case HardwareStatus.Extraida:
                        switch (PropiedadesCara[CaraEncuestada].EstadoDespacho)
                        {
                            //Surtidor a punto de comenzar Despacho
                            case WorkStatus.ComienzoCarga:
                                break;
                            //Surtidor en Despacho
                            case WorkStatus.EnCarga:
                                if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.DevelcoDespacho)
                                {
                                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoDespacho;
                                    PropiedadesCara[CaraEncuestada].Despacho = true; // Para controlar que la venta si se realizó. DCf -- 17/02/2012

                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Cara en Despacho: ");
                                    SWRegistro.Flush();
                                }

                                break;
                            //FIN DE DESPACHOSTATUS: Si el estado el estado de Despacho es FINAL DE CARGA
                            case WorkStatus.FinalDeCarga:
                                if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.DevelcoFinDespachoStatus)
                                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoFinDespachoStatus;
                                break;
                            case WorkStatus.NoEnCarga:
                                //POR AUTORIZAR
                                if (!PropiedadesCara[CaraEncuestada].AutorizarCara &&
                                    !PropiedadesCara[CaraEncuestada].EsVentaParcial &&
                                    (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.DevelcoEspera ||
                                    PropiedadesCara[CaraEncuestada].Estado == EstadoCara.DevelcoIndeterminado))
                                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoPorAutorizar;

                                //AUTORIZADA
                                else if (PropiedadesCara[CaraEncuestada].AutorizarCara &&
                                    PropiedadesCara[CaraEncuestada].Estado == EstadoCara.DevelcoPorAutorizar)
                                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoAutorizada;

                                ////FIN DE DESPACHO STATUS //se comento para pruebas  prueba 19/11/2011
                                //else if (!PropiedadesCara[CaraEncuestada].AutorizarCara &&
                                //    PropiedadesCara[CaraEncuestada].EsVentaParcial)
                                //{
                                //    if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.DevelcoFinDespachoStatus)
                                //        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.DevelcoFinDespachoStatus;
                                //}
                                break;
                        }
                        break;
                }

                //Registra el estado de la cara
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Estado|Hardware Status: " +
                        PropiedadesCara[CaraEncuestada].EstadoHardware);
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Estado|Work Status: " +
                        PropiedadesCara[CaraEncuestada].EstadoDespacho);
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Estado|Status Pulsador: " +
                        PropiedadesCara[CaraEncuestada].Pulsador);
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Estado|Estado Consolidado: " +
                        PropiedadesCara[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ConsolidarEstado: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ALMACENA INFORMACION ADICIONAL DEL SURTIDOR (PRECIO, DENSIDAD, RANGO, VERSION DE FIRMWARE
        private void RecuperarStatusExtendido()
        {
            try
            {
                //Establece la potencia de 10 divisora de Precio e Importe para la cara IMPAR
                if (PropiedadesCara[CaraImpar].FactorImporte == 0 ||
                    PropiedadesCara[CaraImpar].FactorPrecio == 0)
                {
                    switch (TramaRx[17] & 0x03)
                    {
                        case 1:
                            PropiedadesCara[CaraImpar].FactorPrecio = 1000;
                            PropiedadesCara[CaraImpar].FactorImporte = 100;
                            break;
                        case 2:
                            PropiedadesCara[CaraImpar].FactorPrecio = 100;
                            PropiedadesCara[CaraImpar].FactorImporte = 10;
                            break;
                        case 3:
                            PropiedadesCara[CaraImpar].FactorPrecio = 10;
                            PropiedadesCara[CaraImpar].FactorImporte = 1;
                            break;
                        default:
                            PropiedadesCara[CaraImpar].FactorPrecio = 1;
                            PropiedadesCara[CaraImpar].FactorImporte = 1;
                            break;
                    }
                }
                if (PropiedadesCara[CaraImpar].FactorTotalizador == 0)
                    PropiedadesCara[CaraImpar].FactorTotalizador = 100;

                if (PropiedadesCara[CaraImpar].FactorVolumen == 0)
                    PropiedadesCara[CaraImpar].FactorVolumen = 100;

                //Almacena el precio del Surtidor               
                PropiedadesCara[CaraImpar].PrecioCara = ObtenerValor(TramaRx, 8, 11) / PropiedadesCara[CaraImpar].FactorPrecio;



                //Establece la potencia de 10 divisora de Precio e Importe para la cara PAR                                
                if (PropiedadesCara[CaraPar].FactorImporte == 0 ||
                    PropiedadesCara[CaraPar].FactorPrecio == 0)
                {
                    switch (TramaRx[17] & 0x03)
                    {
                        case 1:
                            PropiedadesCara[CaraPar].FactorPrecio = 1000;
                            PropiedadesCara[CaraPar].FactorImporte = 100;
                            break;
                        case 2:
                            PropiedadesCara[CaraPar].FactorPrecio = 100;
                            PropiedadesCara[CaraPar].FactorImporte = 10;
                            break;
                        case 3:
                            PropiedadesCara[CaraPar].FactorPrecio = 10;
                            PropiedadesCara[CaraPar].FactorImporte = 1;
                            break;
                        default:
                            PropiedadesCara[CaraPar].FactorPrecio = 1;
                            PropiedadesCara[CaraPar].FactorImporte = 1;
                            break;
                    }
                }
                if (PropiedadesCara[CaraPar].FactorTotalizador == 0)
                    PropiedadesCara[CaraPar].FactorTotalizador = 100;

                if (PropiedadesCara[CaraPar].FactorVolumen == 0)
                    PropiedadesCara[CaraPar].FactorVolumen = 100;

                //Almacena el precio del Surtidor               
                PropiedadesCara[CaraPar].PrecioCara = PropiedadesCara[CaraImpar].PrecioCara;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarStatusExtendido: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //RECUPERA DATOS DE LOS TOTALIZADORES POR CARA
        private void RecuperarTotalizadores()
        {
            try
            {
                //Si se requiere los valores de los totalizadores de la Cara Impar
                if (PropiedadesCara[CaraImpar].TomarLectura)
                {
                    PropiedadesCara[CaraImpar].Lectura =
                        ObtenerValor(TramaRx, 13, 20) / PropiedadesCara[CaraImpar].FactorTotalizador;

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Proceso|Toma de Totalizador: " +
                    PropiedadesCara[CaraImpar].Lectura);
                    SWRegistro.Flush();
                }
                //Si se requiere los valores de los totalizadores de la Cara Par
                else if (PropiedadesCara[CaraPar].TomarLectura)
                {
                    PropiedadesCara[CaraPar].Lectura =
                        ObtenerValor(TramaRx, 13, 20) / PropiedadesCara[CaraPar].FactorTotalizador;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Proceso|Toma de Totalizador: " +
                    PropiedadesCara[CaraPar].Lectura);
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarTotalizadores: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //DETERMINA SI EL COMANDO ENVIADO AL SURTIDOR FUE ACEPTADO O RECHAZADO
        private void RecuperarConfirmacion()
        {
            try
            {
                ComandoAceptado = false;
                if (TramaRx[4] == 0x06)
                    ComandoAceptado = true;
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Proceso|Comando " + ComandoEnviado +
                        " no aceptado: " + TramaRx[4]);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|RecuperarConfirmacion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                //Solamente ingresa a esta parte de código cuando no se ha inicializado la cara (inicio de programa)
                if (PropiedadesCara[CaraImpar].CaraInicializada == false)
                {
                    //Determina el Rango (factor de division) y el precio
                    if (ProcesoEnvioComando(ComandosSurtidor.PedidoStatusExtendido))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Proceso|Rango: " + TramaRx[17] + " - Precio: " +
                            PropiedadesCara[CaraImpar].PrecioCara + " - Firmware: V" + TramaRx[22] + "." + TramaRx[23] + " R" + TramaRx[24]);
                        SWRegistro.Flush();

                        PropiedadesCara[CaraImpar].CaraInicializada = true;
                        PropiedadesCara[CaraPar].CaraInicializada = true;
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Surtidor no inicializado");
                        SWRegistro.Flush();
                    }
                }
                if (PropiedadesCara[CaraImpar].CaraInicializada)
                {
                    TomarAccionCara(CaraImpar);
                    TomarAccionCara(CaraPar);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|TomarAccion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //TOMAR LA ACCION DEPENDIENDO DEL ESTADO CONSOLIDADO DE CADA UNA DE LAS CARAS(LINEAS)
        private void TomarAccionCara(byte CaraEncuestada)
        {
            try
            {
                String PuertoAImprimir;//Puerto utilizado por el autorizador para imprimir mensajes de error

                //Dependiendo del Estado en que se encuentre la cara, toma la accion respectiva
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    /***************************ESTADO EN ESPERA***************************/
                    case EstadoCara.DevelcoEspera:
                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Inactivada en estado de Espera");
                            SWRegistro.Flush();

                           IniciarCambioTarjeta( CaraEncuestada,  PuertoAImprimir);

                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante la inactivacion
                            if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Cara Inactivada";
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                                {
                                    bool EstadoTurno = false;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                    SWRegistro.Flush();

                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                }
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                                {
                                    bool EstadoTurno = true;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                    SWRegistro.Flush();

                                    CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                                }
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            }
                            //Sale del Caso si se inactiva
                            break;
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            if (TomarLecturaActivacionCara(CaraEncuestada))
                            {
                                //Inicializa bandera que indica la activación de una cara
                                PropiedadesCara[CaraEncuestada].ActivarCara = false;
                                //Instancia Array para reportar las lecturas
                                System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                                ArrayLecturas.CopyTo(LecturasEnvio);

                                //Lanza Evento para reportar las lecturas después de un cambio de tarjeta
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Lecturas para Activación de Cara: " +
                                    LecturasEnvio);
                                SWRegistro.Flush();
                                LecturasCambioTarjeta( LecturasEnvio);
                            }
                        }

                        //Informa cambio de estado
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa cara en Espera");
                            SWRegistro.Flush();
                           CaraEnReposo(CaraEncuestada,mangueraColgada);

                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Apertura/Cierre: " +
                        //    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno + "/" + PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno);
                        //SWRegistro.Flush();

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno ||
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            LecturaAperturaCierre(CaraEncuestada);

                        //Desautoriza continuamente la Cara
                        DesautorizarCara(CaraEncuestada, 255);
                        break;

                    /***************************ESTADO EN DESPACHO***************************/
                    case EstadoCara.DevelcoDespacho:
                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            //Inicializa bandera que indica la activación de una cara
                            PropiedadesCara[CaraEncuestada].ActivarCara = false;
                            //Se inactiva Cara nuevamente, para restringir la activación sólo a estado de Espera
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en despacho";
                            bool Imprime = true;
                            bool Terminal = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            break;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (!PropiedadesCara[CaraEncuestada].EsVentaParcial)
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                        //Pedir Parciales de Venta
                        if (ProcesoEnvioComando(ComandosSurtidor.PedidoDespacho))
                        {
                            string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N2");//  Convert.ToString(Importe[CodigoSurtidor - 1][Linea]);
                            string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N2");//Convert.ToString(Volumen[CodigoSurtidor - 1][Linea]);
                            VentaParcial( CaraEncuestada,  strTotalVenta,  strVolumen);
                        }

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        break;

                    /***************************ESTADO FIN DESPACHO HARDWARE***************************/
                    case EstadoCara.DevelcoFinDespachoHardware:
                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en Fin de Venta Hardware";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            //Inicializa bandera que indica la activación de una cara
                            PropiedadesCara[CaraEncuestada].ActivarCara = false;
                            //Se inactiva Cara nuevamente, para restringir la activación sólo a estado de Espera
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en Fin de Venta Hardware";
                            bool Imprime = true;
                            bool Terminal = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            break;
                        }

                        //Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            //Variable que indica a que manguera fue enviado el Comando Fin de Venta                        
                            //if ((Importe[CodigoSurtidor - 1][Linea] != 0) || (Volumen[CodigoSurtidor - 1][Linea]) != 0)
                            ProcesoFindeVentaStatus(CaraEncuestada);

                            //Reset de Variables de Control                        
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                            PropiedadesCara[CaraEncuestada].Despacho = false;

                        }
                        //Desautoriza continuamente la Cara
                        DesautorizarCara(CaraEncuestada, 255);

                        //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                        if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                        {
                            string MensajeErrorLectura = "Cara en despacho. Fin de venta.";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        break;

                    /***************************ESTADO FIN DESPACHO STATUS***************************/
                    case EstadoCara.DevelcoFinDespachoStatus:
                        //Si la venta no ha sido finalizada, se ejecuta proceso para finalizarla
                        /*if (VentaFinalizada[CodigoSurtidor - 1][Linea] == false)
                        {
                            //Variable que indica a que manguera fue enviado el Comando Fin de Venta
                            FinalizarVenta[CodigoSurtidor - 1][Linea] = true;
                            ProcesoFindeVentaStatus(Linea);
                            //Reset de Variables de Control
                            FinalizarVenta[CodigoSurtidor - 1][Linea] = false;
                            VentaFinalizada[CodigoSurtidor - 1][Linea] = true;
                        }*/

                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en Fin de Venta Status";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            //Inicializa bandera que indica la activación de una cara
                            PropiedadesCara[CaraEncuestada].ActivarCara = false;
                            //Se inactiva Cara nuevamente, para restringir la activación sólo a estado de Espera
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en Fin de Venta Status";
                            bool Imprime = true;
                            bool Terminal = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            break;
                        }

                        //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                        if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                        {
                            string MensajeErrorLectura = "Manguera Fin de Despacho. Colgar Manguera";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                ///////////////////////////////////////////////////////////////////////////////////////////////////////////
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                                ///////////////////////////////////////////////////////////////////////////////////////////////////////////
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                ///////////////////////////////////////////////////////////////////////////////////////////////////////////
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                                ///////////////////////////////////////////////////////////////////////////////////////////////////////////
                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Desautoriza continuamente la Cara
                        DesautorizarCara(CaraEncuestada, 255);
                        break;

                    /***************************ESTADO POR AUTORIZAR***************************/
                    case EstadoCara.DevelcoPorAutorizar:
                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        /*if ((TomarTotalesApertura[CodigoSurtidor - 1][0] == true) || (TomarTotalesCierre[CodigoSurtidor - 1][1] == true))
                            LecturaAperturaCierre(Linea);*/

                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " en intento de Autorización";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            //Inicializa bandera que indica la activación de una cara
                            PropiedadesCara[CaraEncuestada].ActivarCara = false;
                            //Se inactiva Cara nuevamente, para restringir la activación sólo a estado de Espera
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " en intento de Autorizacion";
                            bool Imprime = true;
                            bool Terminal = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            break;
                        }

                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            !PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion");
                            //SWRegistro.Flush();
                            //string Lectura = PropiedadesCara[CaraEncuestada].Lectura.ToString("N3");
                            //AutorizacionRequerida( CaraEncuestada);

                            int IdProducto = PropiedadesCara[CaraEncuestada].ListaGrados[0].IdProducto;
                            int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD;
                            string Lectura = PropiedadesCara[CaraEncuestada].ListaGrados[0].Lectura.ToString("N3");
                            string guid = "";
                            AutorizacionRequerida(CaraEncuestada, IdProducto, IdManguera, Lectura, guid);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion");
                            SWRegistro.Flush();
                        }

                        //Desautoriza continuamente la Cara
                        DesautorizarCara(CaraEncuestada, 255);

                        //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                        if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                        {
                            string MensajeErrorLectura = "Manguera Descolgada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }
                        break;

                    /***************************ESTADO AUTORIZADA***************************/
                    case EstadoCara.DevelcoAutorizada:
                        //EGV:Si la cara se va a Inactivar
                        if (PropiedadesCara[CaraEncuestada].InactivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            string Mensaje = "No se puede ejecutar inactivación: Cara " + CaraEncuestada + " Autorizada";
                            bool Imprime = true;
                            bool Terminal = false;
                            PropiedadesCara[CaraEncuestada].InactivarCara = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                        }

                        //EGV:Si la cara se va a activar
                        if (PropiedadesCara[CaraEncuestada].ActivarCara)
                        {
                            PuertoAImprimir = PropiedadesCara[CaraEncuestada].PuertoParaImprimir;

                            //Inicializa bandera que indica la activación de una cara
                            PropiedadesCara[CaraEncuestada].ActivarCara = false;
                            //Se inactiva Cara nuevamente, para restringir la activación sólo a estado de Espera
                            PropiedadesCara[CaraEncuestada].Activa = false;

                            string Mensaje = "No se puede ejecutar activación: Cara " + CaraEncuestada + " Autorizada";
                            bool Imprime = true;
                            bool Terminal = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|" + Mensaje);
                            SWRegistro.Flush();

                            ExcepcionOcurrida( Mensaje,  Imprime,  Terminal,  PuertoAImprimir);
                            break;
                        }

                        //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                        if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                        {
                            string MensajeErrorLectura = "Cara en despacho. Inicio de venta";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                                SWRegistro.Flush();

                                CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        if (PropiedadesCara[CaraEncuestada].AutorizarCara)
                        {
                            /*- Fecha de Inclusión: 2008/01/23 12:00 -*/
                            //Garantiza toma de lecturas iniciales
                            //Se sugiere incluir todo el código dentro del if (ProcesoEnvioComando(ComandosSurtidor.PedidoTotales))                            
                            PropiedadesCara[CaraEncuestada].TomarLectura = true;
                            ProcesoEnvioComando(ComandosSurtidor.PedidoTotales);
                            PropiedadesCara[CaraEncuestada].TomarLectura = false;
                            /*--*/

                            //Obtiene la Lectura Inicial de la Venta
                            string strLecutrasVolumen = Convert.ToString(PropiedadesCara[CaraEncuestada].Lectura);

                            /*- Fecha de Inclusión: 2008/03/18 10:40 -*/
                            //Almacena el valor de la lectura inicial.  Histórico
                            PropiedadesCara[CaraEncuestada].LecturaInicialVenta =
                                PropiedadesCara[CaraEncuestada].Lectura;
                            /*--*/

                            //Reporta lectura inicial
                            LecturaInicialVenta( CaraEncuestada,  strLecutrasVolumen);

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Informa lectura Inicial de Venta: " + strLecutrasVolumen);
                            SWRegistro.Flush();

                            //Limpia variables de parciales
                            PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                            PropiedadesCara[CaraEncuestada].Volumen = 0;

                            //Autoriza el despacho enviando un TimeOut de 0 segundos
                            DesautorizarCara(CaraEncuestada, 0);
                            if (ComandoAceptado == true)
                            {
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                //WBeleno: Fecha de Inclusión: 2010/06/18 15:45
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;
                            }
                        }
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|TomarAccionLinea: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CAMBIA EL PRECIO DE LA CARA
        private bool CambiarPrecio()
        {
            try
            {
                int Reintentos;

                //Almacena el Precio Actual de la cara en el Vector
                if (ProcesoEnvioComando(ComandosSurtidor.PedidoStatusExtendido))
                {
                    //Analiza si se debe cambiar el precio base de la cara.
                    if (PropiedadesCara[CaraImpar].PrecioCara !=
                        PropiedadesCara[CaraImpar].ListaGrados[0].PrecioNivel1)
                    {
                        Reintentos = 0;
                        do
                        {
                            ComandoAceptado = false;
                            if (ProcesoEnvioComando(ComandosSurtidor.SetPrecio))
                                Reintentos += 1;
                        } while ((ComandoAceptado == false) && (Reintentos <= 3));

                        //Evalúa si el comando Cambio de Precio fue aceptado
                        if (ComandoAceptado)
                        {
                            //Si el comando fue aceptado, cambia las variables respectivas
                            //Cara Impar
                            PropiedadesCara[CaraImpar].PrecioCara = PropiedadesCara[CaraImpar].ListaGrados[0].PrecioNivel1;

                            //Cara Par
                            PropiedadesCara[CaraPar].PrecioCara = PropiedadesCara[CaraImpar].PrecioCara;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Proceso|Precio establecido con exito: " + PropiedadesCara[CaraImpar].PrecioCara);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Proceso|Precio establecido con exito: " + PropiedadesCara[CaraPar].PrecioCara);
                            SWRegistro.Flush();

                            //Devuelve True para indicar que el cambio de precio fue satisfactorio
                            return true;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Fallo|No se pudo establecer nuevo precio (Comando no aceptado): " +
                                PropiedadesCara[CaraImpar].PrecioCara);
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Fallo|No se pudo establecer nuevo precio (Comando no aceptado): " +
                                PropiedadesCara[CaraPar].PrecioCara);
                            SWRegistro.Flush();
                            //Devuelve False para indicar que el cambio de precio NO fue satisfactorio
                            return false;
                        }
                    }
                    //Si el precio en el Surtidor es igual al de la Base de Datos regresa true
                    else
                        return true;
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraImpar + "|Fallo|No se pudo establecer nuevo precio (fallo en comando): " +
                                PropiedadesCara[CaraImpar].PrecioCara);
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraPar + "|Fallo|No se pudo establecer nuevo precio (fallo en comando): " +
                        PropiedadesCara[CaraPar].PrecioCara);
                    SWRegistro.Flush();
                    //Devuelve False para indicar que el cambio de precio NO fue satisfactorio
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|CambiarPrecio: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //PARA TOMAR LECTURAS DE APERTURA Y/O CIERRE DE TURNO
        private void LecturaAperturaCierre(byte CaraEncuestada)
        {
            try
            {
                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();
                System.Array LecturasEnvio;


                //Define variable para reportar lecturas iniciales y/o finales de cada cara de un surtidor determinado
                PropiedadesCara[CaraEncuestada].TomarLectura = true;

                if (ProcesoEnvioComando(ComandosSurtidor.PedidoTotales))
                {
                    //Setea elemento que indica que las lecturas deben tomarse
                    PropiedadesCara[CaraEncuestada].TomarLectura = false;

                    string strLecturaTurno = Convert.ToString(PropiedadesCara[CaraEncuestada].Lectura);

                    //Almacena las lecturas en la lista
                    ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[0].MangueraBD) + "|" +
                        Convert.ToString(PropiedadesCara[CaraEncuestada].Lectura));
                    LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                    ArrayLecturas.CopyTo(LecturasEnvio);

                    //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                    {

                        //Reporta Lecturas Finales de Cara
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Reporta Lectura Final de Turno: " +
                            PropiedadesCara[CaraEncuestada].Lectura);
                        SWRegistro.Flush();

                        LecturaTurnoCerrado(LecturasEnvio);
                       // oEvento.InformarLecturaFinalTurno(LecturasEnvio);
                        //oEvento.InformarLecturaFinalTurno( CaraEncuestada,  strLecturaTurno);

                        PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                    }

                    //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                    {
                        //Reporta Lecturas Iniciales de Cara

                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Evento|Reporta Lectura Inicial de Turno: " +
                            PropiedadesCara[CaraEncuestada].Lectura);
                        SWRegistro.Flush();

                        //oEvento.InformarLecturaInicialTurno( CaraEncuestada,  strLecturaTurno);
                        LecturaTurnoAbierto( LecturasEnvio);



                        PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                        //Si hay cambio de precio pendiente (precio base: PrecioEDS), lo aplica
                        CambiarPrecio();
                    }
                }
                else
                {
                    //Para reportar fallo en la toma de lectura de cierre/apertura de turno
                    if (!PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno)
                    {
                        string MensajeErrorLectura = "Error en comunicación con Surtidor";
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno)
                        {
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Inicio de Turno: " + MensajeErrorLectura);
                            SWRegistro.Flush();

                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Reporta Cancelación de Fin de Turno: " + MensajeErrorLectura);
                            SWRegistro.Flush();

                            CancelarProcesarTurno( CaraEncuestada,  MensajeErrorLectura,  EstadoTurno);
                        }
                        //Se establece valor de la variable para que indique que ya fue reportado el error
                        PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|LecturaAperturaCierre: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //EGV:PARA TOMAR LECTURAS PARA ACTIVACIÓN DE CARA
        private bool TomarLecturaActivacionCara(byte CaraEncuestada)
        {
            try
            {
                //Inicializa Variables a utilizar
                int Reintentos = 0;
                ArrayLecturas = new System.Collections.ArrayList();

                //Se resetea la lectura de cada grado de la cara                
                PropiedadesCara[CaraEncuestada].Lectura = 0;

                PropiedadesCara[CaraEncuestada].TomarLectura = true;

                //Realiza hasta tres reintentos de toma de lecturas
                do
                {
                    Reintentos += 1;

                    if (!ProcesoEnvioComando(ComandosSurtidor.PedidoTotales))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Fallo|No se pudo tomar lecturas para activación de cara");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraEncuestada + "|Proceso|Lectura de activación: " + PropiedadesCara[CaraEncuestada].Lectura);
                        SWRegistro.Flush();

                        ArrayLecturas.Add(CaraEncuestada + "|" + PropiedadesCara[CaraEncuestada].Lectura);

                        //Si el proceso de toma de lecturas fue exitoso, devuelve el arreglo con las lecturas
                        return true;
                    }
                } while (Reintentos <= 3);

                //Si el proceso no fue exitoso, la función devuelve False
                return false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|TomarLecturaActivacionCara: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //REALIZA PROCESO DE FIN DE VENTA
        private void ProcesoFindeVentaStatus(byte Cara)
        {
            try
            {
                //Inicializacion de variables
                int Reintentos = 0;

                PropiedadesCara[Cara].TotalVenta = 0;
                PropiedadesCara[Cara].Volumen = 0;

                //Obtiene los Valores Finales de la Venta (Pesos y Metros cubicos despachados)
                do
                {
                    if (!ProcesoEnvioComando(ComandosSurtidor.PedidoDespacho))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Fallo|Error en Comando PedidoDespacho");
                        SWRegistro.Flush();
                    }
                    Reintentos += 1;
                } while (PropiedadesCara[Cara].TotalVenta == 0 &&
                    PropiedadesCara[Cara].Volumen == 0 && Reintentos <= 2);

                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Importe: " + PropiedadesCara[Cara].TotalVenta +
                    " - Volumen: " + PropiedadesCara[Cara].Volumen);
                SWRegistro.Flush();

                //Obtiene la Lectura Final de la Venta
                PropiedadesCara[Cara].TomarLectura = true;
                ProcesoEnvioComando(ComandosSurtidor.PedidoTotales);
                PropiedadesCara[Cara].TomarLectura = false;

                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Lectura Final de Venta: " + PropiedadesCara[Cara].Lectura);
                SWRegistro.Flush();

                PropiedadesCara[Cara].LecturaFinalVenta = PropiedadesCara[Cara].Lectura;

                //WBeleno: Modificado el 2010/07/23 07:43
                //Si las lecturas inicial y final de venta son iguales realiza una confirmación
                if (PropiedadesCara[Cara].LecturaFinalVenta ==
                    PropiedadesCara[Cara].LecturaInicialVenta)
                {
                    //DCF 15/02/2012 15:29 se quita esta confirmación por que los surtidores no entregan Lf pero si entregan Importe y Volumen
                    ////Tiempo de espera por si el Surtidor no ha rescado el totalizador
                    ////Thread.Sleep(1000);
                    ////Obtiene la Lectura Final de la Venta (Comando Parcial)
                    //TomarParcialTotalizador = true;
                    //PropiedadesCara[Cara].TomarLectura = true;
                    //ProcesoEnvioComando(ComandosSurtidor.PedidoTotales);
                    //TomarParcialTotalizador = false;
                    //PropiedadesCara[Cara].TomarLectura = false;

                    //SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Confirmación Lectura Parcial de Venta: " + PropiedadesCara[Cara].Lectura);
                    //SWRegistro.Flush();

                    //PropiedadesCara[Cara].LecturaFinalVenta = PropiedadesCara[Cara].Lectura;

                    ////Si la lectura final se confirma como igual a la lectura inicial, se considera una venta en 0
                    //if (PropiedadesCara[Cara].LecturaFinalVenta ==
                    //    PropiedadesCara[Cara].LecturaInicialVenta)
                    //{
                        //PropiedadesCara[Cara].Volumen = 0; // si se realiza venta 
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Lectura Final igual a Lectura Inicial");
                        SWRegistro.Flush();
                    //}


                        if (PropiedadesCara[Cara].Despacho == false)
                        {
                            PropiedadesCara[Cara].Volumen = 0;

                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|La Cara no realizo Despacho.  Se forzó Volumen = 0");
                            SWRegistro.Flush();
                        }

                }
                else
                {
                    //Si no se ha reiniciado el sistema, el valor de LecturaInicial es diferente de 0
                    if (PropiedadesCara[Cara].LecturaInicialVenta > 0)
                    {
                        if (PropiedadesCara[Cara].LecturaFinalVenta > 0 &&
                            PropiedadesCara[Cara].LecturaFinalVenta > PropiedadesCara[Cara].LecturaInicialVenta)
                        {
                            /*Se compara el valor de Volumen Calculado con el valor de Volumen Recibido.
                             * La diferencia no debe exceder el (+/-) 1%.  
                             * Se da mayor credibilidad al calculado por lecturas*/

                            //Se crea variable temporal
                            decimal VolumenCalculado = PropiedadesCara[Cara].LecturaFinalVenta -
                                PropiedadesCara[Cara].LecturaInicialVenta;

                            if (PropiedadesCara[Cara].Volumen < VolumenCalculado - 1 ||
                                PropiedadesCara[Cara].Volumen > VolumenCalculado + 1)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara +
                                    "|Proceso|Volumen Reportado por surtidor (" + Convert.ToString(PropiedadesCara[CaraPar].Volumen) +
                                    ") no corresponde con diferencia de lecturas (" + Convert.ToString(VolumenCalculado) + ")");
                                SWRegistro.Flush();
                                PropiedadesCara[Cara].Volumen = VolumenCalculado;
                                PropiedadesCara[Cara].TotalVenta = VolumenCalculado *
                                    PropiedadesCara[Cara].PrecioCara;
                            }
                        }
                        else
                        {
                            PropiedadesCara[Cara].LecturaFinalVenta =
                                PropiedadesCara[Cara].LecturaInicialVenta + PropiedadesCara[Cara].Volumen;
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara +
                                "|Lectura Final de Venta en 0 o menor que Lectura Inicial. Lectura Calculada: " + PropiedadesCara[Cara].LecturaFinalVenta);
                            SWRegistro.Flush();
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Lectura Inicial de Venta en 0");
                        SWRegistro.Flush();
                    }
                }

                //Si se realizó una venta con valores de m3 y $ mayor que cero
                if (PropiedadesCara[Cara].Volumen != 0)
                {
                    //Revisa importe
                    if (PropiedadesCara[Cara].TotalVenta == 0)
                    {
                        PropiedadesCara[Cara].TotalVenta = PropiedadesCara[Cara].Volumen *
                            PropiedadesCara[Cara].PrecioCara;
                        SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Importe recibido en Cero. Calculado: " +
                            PropiedadesCara[Cara].TotalVenta);
                        SWRegistro.Flush();
                    }

                    //Asigna Valores de Fin de Venta para disparar evento
                    string strTotalVenta = PropiedadesCara[Cara].TotalVenta.ToString("N2");// Convert.ToString(Importe[CodigoSurtidor - 1][Linea]);
                    string strPrecio = PropiedadesCara[Cara].PrecioCara.ToString("N2");// Convert.ToString(Precio[CodigoSurtidor - 1]);
                    string strLecutrasVolumen = PropiedadesCara[Cara].LecturaFinalVenta.ToString("N2"); //Convert.ToString(Totalizadores[CodigoSurtidor - 1][Linea]);
                    string strVolumen = PropiedadesCara[Cara].Volumen.ToString("N2"); //Convert.ToString(Volumen[CodigoSurtidor - 1][Linea]);
                    string strLecturaInicialVenta = PropiedadesCara[Cara].LecturaInicialVenta.ToString("N3");
                    string PresionLLenado = "0";
                    byte bytProducto = Convert.ToByte(PropiedadesCara[Cara].ListaGrados[0].IdProducto);
                    int IdManguera = PropiedadesCara[Cara].ListaGrados[0].MangueraBD;

                    //Dispara evento al programa principal si la venta es diferente de 0
                    VentaFinalizada(Cara, strTotalVenta,  strPrecio,  strLecutrasVolumen,  strVolumen, Convert.ToString(bytProducto),  IdManguera,   PresionLLenado,  strLecturaInicialVenta);
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Fin de Venta. Total Venta: " + strTotalVenta +
                         " - Precio: " + strPrecio + " - Volumen: " + strVolumen + " - Lectura Final: " + strLecutrasVolumen + " - Lectura Inicial: " + strLecturaInicialVenta);



                }
                else
                {
                    /*- Fecha de Inclusión: 2008/03/22 12:00 -*/
                    VentaInterrumpidaEnCero( Cara);
                    PropiedadesCara[Cara].EsVentaParcial = false;
                    /*--*/
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Recibida Venta 0 en Fin de Venta Status");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ProcesoFindeVentaStatus: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void DesautorizarCara(byte CaraEncuestada, byte TimeOut)
        {
            try
            {
                int Reintentos = 0;
                do
                {
                    this.TimeOut = TimeOut;
                    PropiedadesCara[CaraEncuestada].DesautorizarDespacho = true;
                    Reintentos += 1;
                    if (ProcesoEnvioComando(ComandosSurtidor.Desautorizar))
                        PropiedadesCara[CaraEncuestada].DesautorizarDespacho = false;
                } while ((ComandoAceptado == false) && (Reintentos < 2));
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|DesautorizarCara: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region METODOS AUXILIARES

        //ANALIZA LA INTEGRIDAD Y CORRESPONDENCIA DE LA TRAMA RESPUESTA
        private void ComprobacionTramaRx()
        {
            try
            {
                //Obtiene el CRC, numero de Surtidor y Codigo de Comando que viene en la trama
                byte CRCRecibido = TramaRx[TramaRx.Length - 1];
                int SurtidorQueResponde = Convert.ToInt16(ObtenerValor(TramaRx, 1, 3));
                byte ComandoRecibido = TramaRx[0];

                //Calcula el CRC
                byte CRCCalculado = CalcularChecksum(TramaRx);

                //1º. Evalúa Byte de Redundancia Cíclica
                if (CRCRecibido == CRCCalculado)
                {
                    //2º. Evalúa Surtidor que responde
                    if (CodigoSurtidor == SurtidorQueResponde)
                    {
                        //3o. Evalua si el Surtidor que reponde, responde al comando enviado
                        if (Convert.ToByte(ComandoEnviado) != ComandoRecibido)
                        {
                            FalloComunicacion = true;
                            SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Comando Enviado: " + ComandoEnviado +
                                ". Comando Recibido: " + ComandoRecibido);
                            SWRegistro.Flush();
                        }
                        else
                            FalloComunicacion = false;
                    }
                    else
                    {
                        FalloComunicacion = true;
                        SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Surtidor encuestado: " + CodigoSurtidor +
                            ". Surtidor que responde: " + SurtidorQueResponde);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    FalloComunicacion = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Fallo|Comando enviado: " + ComandoEnviado + ". Checksum recibido: " + CRCRecibido +
                        " - Checksum real: " + CRCCalculado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ComprobacionTramaRx: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CALCULA EL CHECKSUM (COMPLEMENTO 256) DE LA TRAMA (RECIBIDA O TRANSMITIDA)
        private byte CalcularChecksum(byte[] Trama)
        {
            try
            {
                byte CRC = new byte();
                for (int i = 0; i <= Trama.Length - 2; i++)
                    CRC += Trama[i];
                return Convert.ToByte(CRC);
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|CalcularChecksum: " + Excepcion);
                SWRegistro.Flush();
                return 1;
            }
        }

        //TODO: CAMBIE DE INT A DECIMAL
        //CALCULA EL VALOR OBTENIDO DE LA TRAMA RECIBIDA 
        private decimal ObtenerValor(byte[] Trama, int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal Valor = new decimal();
                for (int i = PosicionInicial; i <= PosicionFinal; i++)
                {
                    if ((Trama[i] >= 0x30) && (Trama[i] <= 0x39))
                        Valor += Convert.ToDecimal((Convert.ToByte(Convert.ToString((char)(Trama[i])), 16)) * Math.Pow(10, PosicionFinal - i));
                }
                return Valor;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|ObtenerValor: " + Excepcion);
                SWRegistro.Flush();
                return 1;
            }
        }

        //INICIALIZA VALORES DE LA MATRIZ PARA TOMA DE LECTURAS
        private void IniciaTomaLecturasTurno(string Surtidores, bool Apertura)
        {
            try
            {
                //Setea banderas de las Caras respectiva de cada surtidor y establece los precios por Grado de cada cara
                string[] bSurtidores = Surtidores.Split('|');
                byte Cara;

                for (int i = 0; i <= bSurtidores.Length - 1; i++)
                {
                    if (!string.IsNullOrEmpty(bSurtidores[i]))
                    {
                        //Organiza banderas de pedido de lecturas para la cara IMPAR
                        Cara = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2 - 1);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(Cara))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Cara].FalloTomaLecturaTurno = false;

                            if (Apertura)
                            {
                                PropiedadesCara[Cara].TomarLecturaAperturaTurno = true;   //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Actualizando variables para toma de Lectura Inicial de Turno");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                PropiedadesCara[Cara].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Actualizando variables para toma de Lectura Final de Turno");
                                SWRegistro.Flush();
                            }
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        Cara = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(Cara))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[Cara].FalloTomaLecturaTurno = false;

                            if (Apertura)
                            {
                                PropiedadesCara[Cara].TomarLecturaAperturaTurno = true;   //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Actualizando variables para toma de Lectura Inicial de Turno");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                PropiedadesCara[Cara].TomarLecturaCierreTurno = true;     //Activa bandera que indica que deben tomarse las Lecturas Finales
                                SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Proceso|Actualizando variables para toma de Lectura Final de Turno");
                                SWRegistro.Flush();
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + Surtidores + "|Excepcion|InicioTomaLecturasTurno: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region EVENTOS DE LA CLASE
        private void oEvento_CambioPrecio( byte Cara,  string Valor)
        {
            try
            {
                PropiedadesCara[CaraImpar].ListaGrados[0].PrecioNivel1 = Convert.ToDecimal(Valor);
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_CambioPrecio: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        {
            try
            {
                //Revisa si la cara pertenece a esta red
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    //Bandera que indica que la cara debe autorizarse para desapchar
                    PropiedadesCara[Cara].AutorizarCara = true;

                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                        " - Tipo de Programacion: " + TipoProgramacion);
                    SWRegistro.Flush();

                    PropiedadesCara[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    //Si viene valor para predeterminar setea banderas
                    if (PropiedadesCara[Cara].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PropiedadesCara[Cara].PredeterminarVolumen = true;
                            PropiedadesCara[Cara].PredeterminarImporte = false;
                        }
                        else
                        {
                            PropiedadesCara[Cara].PredeterminarVolumen = false;
                            PropiedadesCara[Cara].PredeterminarImporte = true;
                        }
                    }
                    else
                    {
                        PropiedadesCara[Cara].PredeterminarVolumen = false;
                        PropiedadesCara[Cara].PredeterminarImporte = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_VentaAutorizada: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        

          
    
        public void Evento_TurnoAbierto( string Surtidores,  string PuertoTerminal,  System.Array Precios)
        {
            try
            {
                //Loguea evento
                SWRegistro.WriteLine(DateTime.Now + "|Evento Recibido|(TurnoAbierto). Surtidores: " + Surtidores);
                SWRegistro.Flush();

                //Diccionario de los productos manejados
                Dictionary<int, Producto> Productos = new Dictionary<int, Producto>();

                //Ciclo que arma Diccionario de Productos
                foreach (string sPreciosProducto in Precios)
                {
                    //Objeto Producto para añadir al Diccionario
                    Producto PrecioProducto = new Producto();

                    string[] vPreciosProducto = sPreciosProducto.Split('|');
                    PrecioProducto.IdProducto = Convert.ToByte(vPreciosProducto[0]);
                    PrecioProducto.PrecioNivel1 = Convert.ToDecimal(vPreciosProducto[1]);
                    PrecioProducto.PrecioNivel2 = Convert.ToDecimal(vPreciosProducto[2]);
                    


                    //Si el prodcuto no existe dentro del diccionario, lo añade
                    if (!Productos.ContainsKey(PrecioProducto.IdProducto))
                        Productos.Add(PrecioProducto.IdProducto, PrecioProducto);
                    else
                    {
                        Productos[PrecioProducto.IdProducto].IdProducto = PrecioProducto.IdProducto;
                        Productos[PrecioProducto.IdProducto].PrecioNivel1 = PrecioProducto.PrecioNivel1;
                        Productos[PrecioProducto.IdProducto].PrecioNivel2 = PrecioProducto.PrecioNivel2;
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
                        if (PropiedadesCara.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraLectura].TomarLecturaAperturaTurno = true;
                            }



                            //Guarda los precios del Producto de cada grado de la cara
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraLectura].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel1 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel1;
                            PropiedadesCara[CaraLectura].ListaGrados[0].PrecioNivel2 =
                                Productos[PropiedadesCara[CaraLectura].ListaGrados[0].IdProducto].PrecioNivel2;
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|Cara|" + CaraLectura + "|Proceso|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

 
        public void Evento_TurnoCerrado( string Surtidores,  string PuertoTerminal)
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Evento|Recibe evento para cierre de turno. Surtidores: " +
                   Surtidores);
                SWRegistro.Flush();
                IniciaTomaLecturasTurno(Surtidores, false); //Indica que las lecturas a tomar son las finales             
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_TurnoCerrado: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        //EGV: EVENTO PARA SOLICITAR INACTIVACION DE CARA
        public void Evento_InactivarCaraCambioTarjeta( byte Cara,  string Puerto)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].InactivarCara = true;
                    PropiedadesCara[Cara].PuertoParaImprimir = Puerto;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe Comando para Inactivar");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_InactivarCaraCambioTarjeta: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        //EGV: EVENTO PARA SOLICITAR ACTIVACION DE CARA
        public void Evento_FinalizarCambioTarjeta( byte Cara)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].ActivarCara = true;
                    PropiedadesCara[Cara].Activa = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Cara + "|Evento|Recibe comando para activacion");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_FinalizarCambioTarjeta: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_CerrarProtocolo()
        {
            SWRegistro.WriteLine(DateTime.Now + "|" + "|Surtidor|" + CodigoSurtidor + "|Evento|Recibe evento de detencion de Protocolo");
            SWRegistro.Flush();
            this.CondicionCiclo = false;
        }

        public void Evento_FinalizarVentaPorMonitoreoCHIP( byte Cara)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Convert.ToString(Cara) + "|Evento|oEvento_FinalizarVentaPorMonitoreoCHIP|Solicitar detener venta");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_FinalizarVentaPorMOnitoreoChip: " + Excepcion);
                SWRegistro.Flush();
            }

        }
        private void oEvento_CambiarDensidad( string predDensidad)
        {
            try
            {
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                {
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = true;
                    SWRegistro.WriteLine(DateTime.Now + "|Cara|" + Propiedad.Cara + "|Evento|Recibe comando de cambio de Densidad: " + predDensidad + ". Comando para Cambiar Densidad");
                    SWRegistro.Flush();
                }
                DensidadEDS = Convert.ToDecimal(predDensidad);
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|Surtidor|" + CodigoSurtidor + "|Excepcion|oEvento_CambiarDensidad: " + Excepcion);
                SWRegistro.Flush();
                byte Cara = 1;
                foreach (RedSurtidor Propiedad in PropiedadesCara.Values)
                    PropiedadesCara[Propiedad.Cara].CambiarDensidad = false;
            }
        }

        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras) 
        {
        
        }

        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }

        public void Evento_CancelarVenta(byte Cara)
        {

        }


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera
        {
        }


   
        #endregion
    }
}


