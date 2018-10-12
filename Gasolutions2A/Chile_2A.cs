
using System; //|Protocolo. Chile_2A 2011.12.15-1000
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables
using POSstation.Protocolos;

namespace POSstation.Protocolos
{
    public class Chile_2A : iProtocolo
    {
        #region EventoDeProtocolo
        public event iProtocolo.CambioMangueraEnVentaGerenciadaEventHandler CambioMangueraEnVentaGerenciada;

        public event iProtocolo.CaraEnReposoEventHandler CaraEnReposo;//Listo***

        public event iProtocolo.VentaFinalizadaEventHandler VentaFinalizada;//Listo***

        public event iProtocolo.LecturaTurnoCerradoEventHandler LecturaTurnoCerrado;//isto****

        public event iProtocolo.LecturaTurnoAbiertoEventHandler LecturaTurnoAbierto;//Listo***

        public event iProtocolo.LecturaInicialVentaEventHandler LecturaInicialVenta;//lISTO***

        public event iProtocolo.VentaParcialEventHandler VentaParcial;//lISTO***

        public event iProtocolo.CambioPrecioFallidoEventHandler CambioPrecioFallido;//lSITO

        public event iProtocolo.CancelarProcesarTurnoEventHandler CancelarProcesarTurno;//lISTO***

        public event iProtocolo.ExcepcionOcurridaEventHandler ExcepcionOcurrida;//Listo***

        public event iProtocolo.VentaInterrumpidaEnCeroEventHandler VentaInterrumpidaEnCero;//Listo***

        public event iProtocolo.AutorizacionRequeridaEventHandler AutorizacionRequerida;//Listo***

        public event iProtocolo.IniciarCambioTarjetaEventHandler IniciarCambioTarjeta;//Listo ***

        public event iProtocolo.LecturasCambioTarjetaEventHandler LecturasCambioTarjeta;///Listo***

        public event iProtocolo.NotificarCambioPrecioMangueraEventHandler NotificarCambioPrecioManguera;//Listo

        #endregion

        #region DECLARACION DE VARIABLES Y DEFINICIONES

        Dictionary<byte, RedSurtidor> PropiedadesCara;        //Diccionario donde se almacenan las Caras y sus propiedades

        public enum ComandoSurtidor
        {

            //Mensajes de Pedido de Informacion
            Estado, // CD1 0X00
            Autorizar, // CD1  0x06
            Reset, //  CD1 0x05
            Pool, //0x20---> luego de recibir el ACK
            Ack, // 0xC0 --- > enviar despues de recivir el Data
            Data, //0x30 ---> Data envio y rx de informacion
            ObtenerPrecio,
            ObtenerVentaDinero,
            ObtenerVentaVolumen,
            ObtenerDatosVenta,
            EstalecerPrecio,
            Predeterminar_Volumen,
            Predeterminar_Importe,
            ObtenerTotalizador,
            ObtenerTotalizadorImporte,
            SuspenderLlenado,
            ReanudarLlenado,

            EnviarACK,

            FinVenta_AF,
            OffEstado_AF,
            contadorError,

            stop,

        }   //Define los COMANDOS que se envian al Surtidor

        ComandoSurtidor ComandoCaras;

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no
        //string AuxiliarLectura;

        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
                //Controla la comunicacion entre las aplicaciones por medio de eventos

        //VARIABLES UTILZADAS PARA LOGUEO TXT

        //Variable que almacen la ruta y el nombre del archivo que guarda inconsistencias en el proceso logico
        string Archivo;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWRegistro;
        //Variable que almacen la ruta y el nombre del archivo que guarda las tramas de transmisión y recepción (Comunicación con Surtidor)
        string ArchivoTramas;
        //Variable utilizada para escribir en el archivo
        StreamWriter SWTramas;
        bool CondicionCiclo = true;


        /// VARIABLES 2A

        string strTramaTx; //  Estructura de la Trama a enviar 

        byte[] ETX = { 0x03 };
        byte[] SF = { 0xFA };

        string CRC_TramaRx;
        int posicion_byte;



   
        int ADR;
        //int CTRL1;
        int CTRL;
        int TNO; //NUmero de Transaccion CD Maestro-DC Esclavo
        int LNG; //Longitud del Dato
        //int TXNum;
        int Datablok1; // Dato a transmitir
        //int NUMDatablok1; //Cantidad de byte que tendra la varible Datablok1
        byte[] Datablok;
        byte[] ArrayCRC = new byte[2];
        //string strTrama;
        string VOLVenta;
        string Importe;
        string PrecioVenta;
        string VOLTotal;
        string VOLTotal1;
        string VOLTotal2;
        //int contaBoquilla;
        string ImpTotal;
        string ImpTotal1;
        string ImpTotal2;

        //int Repeat_Comando;
        //bool repet;
        int Reintentos;

        byte[] adr = new byte[1];
        byte[] ctrl = new byte[1];

        bool TX_Pool;//Utilizado para activar retardo en el envio RX- 50ms -->TX
        string Puerto;

        #endregion

        #region PUNTO DE ARRANQUE
        //PUNTO DE ARRANQUE DE LA CLASE
        public Chile_2A(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {

                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Chile_2A-Tramas(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Chile_2A-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.04.12-000");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.06.09-1456");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.06.10-1611");
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.06.17-1155"); //Envio del Poll 3 veces para rescar la EEPROM
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.06.29-1800"); //toma de totalVolumen 3 veces en caso de que VolF = Vol I
                ////SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.08.03-1645"); ////Nuevo DCF EsVentaParcial = true en manguera colgada. //03-08-2011
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.08.22-1551"); //toma de totalVolumen 3 veces en caso de que VolF = Vol I
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.09.22-1050"); // Ventaparcial true -- proceso fin de venta  I
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.11.04-1721"); // Grado  = 0, diferencias en el  grados entregado por el surtidores  
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.11.08-0927"); // Grado  = 0, diferencias en el  grados entregado por el surtidores  Logueo borrar 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2011.12.21-1500"); // venta calculada y borrar datos de venta anteriro procesofindeventa
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2012.01.25-1109"); //Modificado para disminuir las consulta y bajar los tiempo 25/01/2012. se hace en envio del POLL antes de Obtener el totalizador de volumen 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2012.02.09-1138");//se filtra el ImporteCalculado si es = 0 no se realizo venta  salta a ventas en CERO 09/02/2011 - 11:38
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2012.08.23-1736");//Logueo de factores    
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2012.11.21-1448");// |Excepcion|VerifySizeFile: 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.02.08-1107");// public int _FactorTotalizadorImporte = 1; //Importer para dulex para sacar calculo de venta por totalizador de importes; dos decimales  "/100"
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.02.25-1100");////validacion de la ventas y los valores calculados y entregados por el surtidor:
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.03.05-1754");// PropiedadesCara[CaraEncuestada].FA) //DCF 05-03-2013:// aparece 0XFA en el crec -0x35, 0x10, 0xFa
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.03.06-1754");// Envio de venta parcial !=0 06/03/2013
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.05.21-1015");// 21/05/2013 se envía el reset luego de presentarse este estado "A2Predeterminacio_Alcanzada" -- Cambio de firmware
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.05.24-1452");// PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Predeterminacio_Alcanzada; // se quita este estado no se utiliza //tiempo de espera para escribir sobre el surtidor 50 ms sugerencia del fabricante
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.06.07-1010");// 05-06-2013   ProcesoEnvioComando(ComandoSurtidor.stop)
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.06.11-1131");// se debe enviar el comando reset antes de autorizar 11-06-2013.
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.06.13-1753"); //Cambio en el fin de venta 13-06-2013 DCF 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.07.30-1114"); //Cambio en el fin de venta 30-07-2013  Juan David -- Agrego 300 Milisegundos de Retardo 
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.11.28-1019");//Application.StartupPath
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2013.12.13-0908");//*** Recuperar venta para 2A cambio de estados Despacho - Reposo ***** 2013/12/13 - 0814
               //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2014.05.08-1743");// Convert.ToDecimal(Importe) / PropiedadesCara[CaraEncuestada].FactorImporte;
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2015.11.10-1650");// Convert.ToDecimal(Importe) / PropiedadesCara[CaraEncuestada].FactorImporte;
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2017.07.27-1100");// Tiempos de espera . //DCF para EDE sinlanza Peru RC ingeniero 26/07/2017
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Chile_2A 2018.03.08-1620");//DCF Archivos .txt 08/03/2018  
                SWRegistro.Flush();


                SWRegistro.WriteLine(DateTime.Now + "|Antes de Validar puerto: " + Puerto);

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Puerto COM Close|: " + Puerto);
                    //SWRegistro.Flush();
                    SWRegistro.WriteLine(DateTime.Now + "|Antes de arbrir puerto: " + Puerto );
                    SWRegistro.Flush();
                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Odd;
                    PuertoCom.Open();
                    SWRegistro.WriteLine(DateTime.Now + "|Despues de arbrir puerto: " + Puerto);
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();

                    //SWRegistro.WriteLine(DateTime.Now + "|" + "|Puerto COM OPEN|: " + Puerto);
                    //SWRegistro.Flush();


                }

                //PropiedadesCara es la erencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                SWRegistro.WriteLine(DateTime.Now + "|Numero Elementos" + EstructuraCaras.Count.ToString());               
                SWRegistro.Flush();
                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|Cara: " + oCara.Cara.ToString());
                    SWRegistro.Flush();

                    foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial);

                    //*** Recuperar venta para 2A cambio de estados Despacho - Reposo ***** 2013/12/13 - 0814
                    if (oCara.EsVentaParcial)
                        PropiedadesCara[oCara.Cara].RecuperarVenta = true;
                    else
                        PropiedadesCara[oCara.Cara].RecuperarVenta = false;

                    ////Logueo de factores  
                    SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|FactorVolumen: " + Math.Log10(PropiedadesCara[oCara.Cara].FactorVolumen)
                                 + " - FactorTotalizador: " + Math.Log10(PropiedadesCara[oCara.Cara].FactorTotalizador)
                                 + " - FactorImporte: " + Math.Log10(PropiedadesCara[oCara.Cara].FactorImporte)
                                 + " - FactorPrecio: " + Math.Log10(PropiedadesCara[oCara.Cara].FactorPrecio));


                    SWRegistro.Flush();
                
                }
                SWRegistro.Flush();

                //Variable que determina si la interfaz física de los surtidores añade eco a las tramas recibida
                TramaEco = Eco;

                //Crea el Hilo que ejecuta el recorrido por las caras
                Thread HiloCicloCaras = new Thread(CicloCara);

                //Inicial el hilo de encuesta cíclica
                HiloCicloCaras.Start();
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + PropiedadesCara.Count + "|Excepcion|Wayne: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        public void CicloCara()
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Inicia ciclo de encuesta a " + PropiedadesCara.Count + " caras");
                SWRegistro.Flush();



                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    try
                    {
                        VerifySizeFile();
                        //Ciclo de recorrido por las caras
                        foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
                        {
                            //Si la cara está activa, realizar proceso de encuesta
                            if (ORedCaras.Activa == true)
                            {
                                CaraEncuestada = ORedCaras.Cara;
                                //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                if (ProcesoEnvioComando(ComandoSurtidor.Pool))
                                    TomarAccion();
                                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                                Reintentos = 0;
                            }
                            Thread.Sleep(20);
                        }
                    }

                    catch (Exception Excepcion)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|CondicionCiclo: " + Excepcion);
                        SWRegistro.Flush();
                    }

                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|CicloCara: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        #endregion

        public void VerifySizeFile() //Analiza el tamaño del archivo 
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Chile_2A-Tramas(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }

                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-Chile_2A-Sucesos(" + Puerto + ").txt";
                    SWRegistro = File.AppendText(Archivo);
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|VerifySizeFile: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #region CONSTRUCCIÓN, ENVÍO Y RECEPCIÓN DE TRAMAS DE COMUNICACIÓN

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        public bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 3;// antes 2 DCF

                Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx();

                do
                {
                    EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    if (ComandoCaras != ComandoSurtidor.Ack)   /// mirar                    
                        RecibirInformacion();
                    else
                        FalloComunicacion = false;

                    Reintentos += 1;
                } while (FalloComunicacion == true && Reintentos < MaximoReintento);


                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion)
                {
                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            if (CancelarProcesarTurno != null)
                            {
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            if (CancelarProcesarTurno != null)
                            {
                                CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Perdida de comunicacion en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = true;
                    }

                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Se restablece comunicación con surtidor en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|ProcesoEnviocomando: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        public void ArmarTramaTx()
        {
            try
            {


                ADR = (0X50 + Convert.ToInt16(CaraEncuestada - 1)); //Direccion de la Cara inician en 0 to 16

                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Pool:
                        TimeOut = 100; //50;//100;
                        TramaTx = new byte[3];
                        adr = new byte[1] { Convert.ToByte(ADR) };
                        ctrl = new byte[1] { 0X20 }; //Poll

                        adr.CopyTo(TramaTx, 0);
                        ctrl.CopyTo(TramaTx, 1);
                        SF.CopyTo(TramaTx, 2);
                        break;

                    case ComandoSurtidor.Ack:
                        TimeOut = 50; //20; //100;
                        TramaTx = new byte[3];
                        adr = new byte[1] { Convert.ToByte(ADR) };
                        ctrl = new byte[1] { 0XC0 }; //Poll

                        adr.CopyTo(TramaTx, 0);
                        ctrl.CopyTo(TramaTx, 1);
                        SF.CopyTo(TramaTx, 2);
                        break;

                    case ComandoSurtidor.Estado:
                        TimeOut = 100;// 50;//100;
                        CTRL = 0x30; //Data
                        TNO = 0x01; // Command to Pump, Master to Slave 
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = 0x00; //  ***** Retornar el estado de Pump ********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO    
                        break;


                    case ComandoSurtidor.Reset:
                        TimeOut = 100; //50;//100;
                        CTRL = 0x30; //Data
                        TNO = 0x01; // Command to Pump, Master to Slave 
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = 0x05; //  ***** Reset ********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO   
                        break;


                    case ComandoSurtidor.stop:
                        TimeOut = 100; //50;//100;
                        CTRL = 0x30; //Data
                        TNO = 0x01; // Command to Pump, Master to Slave 
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = 0x08; //  ***** Stop ********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO   
                        break;


                    case ComandoSurtidor.Autorizar:
                        TimeOut = 200; //100;//100;
                        CTRL = 0x30; //Data
                        TNO = 0x01; // Command to Pump, Master to Slave
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = 0x06; // ***** Autorizar ********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO    
                        break;

                    case ComandoSurtidor.ObtenerTotalizador: // Totalizador de volumen CD101
                        TimeOut = 500; //200;//100;

                        //if(TX_Pool)
                        //    TimeOut = 200;

                        CTRL = 0x30; //Data
                        TNO = 0x65; // Totalizador CD101
                        LNG = 0x01; // Dimensión de Data Block   
                        Datablok1 = Convert.ToInt16((PropiedadesCara[CaraEncuestada].GradoCara) + 1);

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO     
                        break;


                    case ComandoSurtidor.ObtenerTotalizadorImporte:
                        TimeOut = 500; //100;//200;
                        CTRL = 0x30; //Data
                        TNO = 0x66; // Totalizador CD102
                        LNG = 0x01; // Dimensión de Data Block   
                        Datablok1 = Convert.ToInt16((PropiedadesCara[CaraEncuestada].GradoCara) + 1);

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO     
                        break;

                    case ComandoSurtidor.ObtenerDatosVenta: //***** Retornar INformacion de Venta ********
                        TimeOut = 500; //200;//400;
                        CTRL = 0x30; //Data
                        TNO = 0x01; // Totalizador CD101
                        LNG = 0x01; // Dimensión de Data Block   
                        Datablok1 = 0x04; //  ***** Retornar INformacion de Venta ********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO     

                        break;

                    case ComandoSurtidor.EstalecerPrecio:
                        TimeOut = 500; //200;
                        CTRL = 0x30; //Data     
                        TNO = 0x05; //CD3 Comando a Pump
                        LNG = 0x03; // Dimensión de Data Block   
                        //Dato Block Datablok1                       
                        string PRI = "0";
                        PRI = Convert.ToString(Convert.ToInt32(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1 * PropiedadesCara[CaraEncuestada].FactorPrecio));
                        Datablok1 = Convert.ToInt32((PRI), 16);
                        //decimal xxx = Convert.ToDecimal(PRI);

                //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + " ComandoCaras: " + ComandoCaras + "|PV : " + PRI
                //            + " |PrecioNivel1 : " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1
                //            + " |FactorPrecio: " + PropiedadesCara[CaraEncuestada].FactorPrecio);
                //SWRegistro.Flush();
                        
                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO  

                        break;

                    case ComandoSurtidor.Predeterminar_Volumen:
                        TimeOut = 500; //200;
                        CTRL = 0x30; //Data     
                        TNO = 0x03; //CD3 Preset Volumen
                        LNG = 0x04; // Dimensión de Data Block   
                        //Dato Block Datablok1          
                        string Volumen = "0";
                        Volumen = Convert.ToString(Convert.ToInt32((PropiedadesCara[CaraEncuestada].ValorPredeterminado) * PropiedadesCara[CaraEncuestada].FactorVolumen)); //?? CONSULTAR EL FACTOR
                        Datablok1 = Convert.ToInt32((Volumen), 16);

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO  
                        break;



                    case ComandoSurtidor.Predeterminar_Importe:
                        TimeOut = 500; //200;
                        CTRL = 0x30; //Data     
                        TNO = 0x04; //CD3 Preset Importe
                        LNG = 0x04; // Dimensión de Data Block   
                        //Dato Block Datablok1                                 
                        string Importe = "0";
                        Importe = Convert.ToString(Convert.ToInt32((PropiedadesCara[CaraEncuestada].ValorPredeterminado) * PropiedadesCara[CaraEncuestada].FactorImporte));
                        Datablok1 = Convert.ToInt32((Importe), 16);

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO  
                        break;

                    case ComandoSurtidor.SuspenderLlenado:
                        TimeOut = 100;
                        CTRL = 0x30; //Data
                        TNO = 0x0E; // Command to Pump, Master to Slave 
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = PropiedadesCara[CaraEncuestada].Cara; //  ***** Cara a Suspender llenado********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO    
                        break;


                    case ComandoSurtidor.ReanudarLlenado:
                        TimeOut = 100;
                        CTRL = 0x30; //Data
                        TNO = 0x0F; // Command to Pump, Master to Slave 
                        LNG = 0x01; // Dimensión de Data Block
                        Datablok1 = PropiedadesCara[CaraEncuestada].Cara; //  ***** Cara a Suspender llenado********

                        Encabezado_TramaTx(); // se Obtiene ADR CTRL TNO LNG DATO    
                        break;




                }


            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + " ComandoCaras: " + ComandoCaras + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Encabezado_TramaTx()
        {

            ADR = (0X50 + Convert.ToInt16(CaraEncuestada - 1)); // Asignacion de la direccion en el canal 1 = 50 + ADR

            if (CTRL == 0x30) // SI se enviara un Data, CTRL = DATA = 0x03
            {
                strTramaTx = Convert.ToString(ADR, 16).PadLeft(2, '0') + Convert.ToString(CTRL, 16).PadLeft(2, '0') + Convert.ToString(TNO, 16).PadLeft(2, '0') +
                             Convert.ToString(LNG, 16).PadLeft(2, '0') + Convert.ToString(Datablok1, 16).PadLeft(LNG * 2, '0'); //  LNG * 2 = NUMDatablok1 debe ser variable para cambio de precio es = 6...

                CalcularCRC(strTramaTx);//Calculo del CRC

                // control para aumentar el tamaño del TX, para que pase de .....FA 78 to ....... 10 FA 78 
                if (PropiedadesCara[CaraEncuestada].FA)
                {
                    TramaTx = new byte[strTramaTx.Length / 2 + 5];//Dimencion de la trama aumentada en byte --  10 FA 78 
                }
                else
                TramaTx = new byte[strTramaTx.Length / 2 + 4];//Dimencion de la trama 


                for (int k = 0, l = 0; l < strTramaTx.Length; k++, l = l + 2) //Armar trama a Enviar
                {
                    TramaTx[k] = Convert.ToByte(strTramaTx.Substring(l, 2), 16);
                }


                if (PropiedadesCara[CaraEncuestada].FA)
                {
                    ArrayCRC.CopyTo(TramaTx, TramaTx.Length - 5);
                    ETX.CopyTo(TramaTx, TramaTx.Length - 2);
                    SF.CopyTo(TramaTx, TramaTx.Length - 1);
                }
                else
                {
                    // 50 30 01 01 00 9F 5C 03 FA     9Fh= CRC16 Low byte, 5Ch= CRC16 high byte.

                    ArrayCRC.CopyTo(TramaTx, TramaTx.Length - 4);
                    ETX.CopyTo(TramaTx, TramaTx.Length - 2);
                    SF.CopyTo(TramaTx, TramaTx.Length - 1);

                }
            }


            if (CTRL == 0x20) // SI se enviara un Poll,
            {
                TramaTx = new byte[3] { Convert.ToByte(ADR), Convert.ToByte(CTRL), Convert.ToByte(SF) };
            }


        }




        public byte[] CalcularCRC(string Trama)
        {
            try
            {
                int CRC = 0x00;
                int Carry;

                for (int i = 0; i < Trama.Length; i += 2)
                {
                    //char Caracter = Convert.ToChar(Trama.Substring(i, 1));
                    string Caracter = (Convert.ToString(Trama.Substring(i, 2))); //Obtiene el caracter
                    int value = Convert.ToInt32(Caracter, 16); // convierte a HEX
                    //char value = Convert.ToChar(Trama.Substring(i, 1));

                    CRC = CRC ^ (int)(value);
                    for (int j = 1; j <= 8; j++)
                    {
                        Carry = CRC & 0x01;
                        CRC >>= 1;
                        if (Carry != 0)
                            CRC = CRC ^ 0xA001;
                    }
                }

                string sCRC = CRC.ToString("X2").PadLeft(4, '0'); //convierte el CRC de tipo INT a tipo String

                ArrayCRC = new byte[2];
               
                
                ArrayCRC[1] = Convert.ToByte(sCRC.Substring(0, sCRC.Length - 2), 16); //convierte el CRC a tipo Byte
                ArrayCRC[0] = Convert.ToByte(sCRC.Substring(sCRC.Length - 2, 2), 16);


                //DCF 16/04/2012 si el Tx xontiene FA
                //Para Aplicar
                //Where CRC value have byte (FA Hex)  put behind  CRC value  10Hex  
                //Egzample 1 ; CRC=1E FA  sended byte will be 1E 10 FA  
                //Egzample 2 ; CRC= FA 78 sended byte will be 10 FA 78  
                //50 30 66 10 01 00 00 58 10 71 00 00 58 10 71 00 00 00 00 00
                //50 30 66 10 01 00 00 58 10 71 00 00 58 10 71 00 00 00 00 00 1E 10 FA 03 FA 

                if (ArrayCRC[0] == 0xFA)
                {

                    ArrayCRC = new byte[3];

                    ArrayCRC[0] = 0x10;
                    ArrayCRC[1] = 0xFA; //convierte el CRC a tipo Byte
                    ArrayCRC[2] = Convert.ToByte(sCRC.Substring(0, sCRC.Length - 2), 16);

                    

                }

                else if (ArrayCRC[1] == 0xFA)
                {
                    ArrayCRC = new byte[3];

                    ArrayCRC[0] = Convert.ToByte(sCRC.Substring(sCRC.Length - 2, 2), 16);
                        ArrayCRC[1] = 0x10;
                            ArrayCRC[2] = 0xFA;

                }

                // control para aumentar el tamaño del TX, para que pase de .....FA 78 to ....... 10 FA 78 
                if (ArrayCRC.Length == 3) 
                {
                    PropiedadesCara[CaraEncuestada].FA = true;
                }
                else
                {
                    PropiedadesCara[CaraEncuestada].FA = false;
                }



                //si aparece FA en algun crc active el salto de confirmacion de CRC
                if ((ArrayCRC[1] == 0xFA && ArrayCRC[0] == TramaRx[TramaRx.Length - 5]) |
                    (ArrayCRC[0] == 0xFA && ArrayCRC[1] == TramaRx[TramaRx.Length - 3]) |
                    PropiedadesCara[CaraEncuestada].FA) //DCF 05-03-2013:// aparece 0XFA en el crec -0x35, 0x10, 0xFa

                {
                    PropiedadesCara[CaraEncuestada].CRC_FA = true;
                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "| CRC con 0xFA");
                    //SWRegistro.Flush();
                  
                   
                }
                else
                {
                    PropiedadesCara[CaraEncuestada].CRC_FA = false;
                }



                return ArrayCRC;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CalcularCRC: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                byte[] RetornoExcepcion = new byte[2];
                return RetornoExcepcion;
            }
        }







        //ENVIA EL COMANDO AL SURTIDOR
        public void EnviarComando()
        {
            try
            {


                if (TX_Pool)
                {
                    Thread.Sleep(10); //tiempo de espera para escribir sobre el surtidor 50 ms sugerencia del fabricante
                    SWTramas.WriteLine("Sleep 10ms");
                    SWTramas.Flush();
                }

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
                    "|" + CaraEncuestada + "|Tx|" + ComandoCaras + "|" + strTrama);

                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                eco = Convert.ToByte(TramaTx.Length); //respuesta del LOOP de Corriente

                //Tiempo muerto mientras el Surtidor Responde
                //Thread.Sleep(TimeOut + 800); //DCF Borra solo para testeo
                Thread.Sleep(TimeOut); //antes + 50

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|EnviarComando: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        public void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                if (!TramaEco)
                    eco = 0;

                ////Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                //int BytesEsperados = 0x0D + eco;

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                //if (Bytes >= BytesEsperados)
                if (Bytes > 0) //Para prueba observacion
                {
                    //Definicion de Trama Temporal
                    byte[] TramaTemporal = new byte[Bytes];

                    //TramaTemporal = new byte[25] { 0x50, 0x30, 0x01, 0x01, 0x05, 0x03, 0x04, 0x00, 0x05, 0x00, 0x01, 0x02, 0x08, 0x00, 0x00, 0x04, 0x82, 0x00, 0x00, 0x24, 0x10, 0x8F, 0xA7, 0x03, 0xFA };
                    //|50|30|65|10|00|00|07|30|79|00|00|07|30|79|00|00|00|00|00|5D|96|03|FA|
                    //TramaTemporal = new byte[3] { 0x54, 0x70, 0xFA };
                    //TramaTemporal = new byte[3] { 0x54, 0xC0, 0xFA };
                    //TramaTemporal = new byte[23] { 0x50, 0x30 , 0x65, 0x10, 0x00, 0x00 , 0x07, 0x30, 0x79, 0x00, 0x00, 0x07, 0x30, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5D, 0x96, 0x03, 0xFA};


                    //TramaTemporal = new byte[25] { 0x50, 0x30, 0x66, 0x10, 0x01, 0x00, 0x00, 0x58, 0x10, 0x71, 0x00, 0x00, 0x58, 0x10, 0x71, 0x00, 0x00, 0x00, 0x0, 0x00, 0x1E, 0x10, 0xFA, 0x03, 0xFA };
                    //TramaTemporal = new byte[15] { 0x55, 0x30, 0x01, 0x01, 0x01, 0x03, 0x04, 0x00, 0x05, 0x00, 0x11, 0xD0, 0x90, 0x03, 0xFA };
                    //TramaTemporal = new byte[24] { 0x55, 0x30, 0x65, 0x10, 0x01, 0x00, 0x06, 0x51, 0x48, 0x10, 0x00, 0x06, 0x51, 0x48, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0xD8, 0xB8, 0x03, 0xFA };
                    //TramaTemporal = new byte[24] { 0x55, 0x30, 0x66, 0x10, 0x01, 0x00, 0x31, 0x15, 0x85, 0x59, 0x00, 0x31, 0x15, 0x85, 0x59, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1E, 0xE7, 0x03, 0xFA };
                    //TramaTemporal = new byte[15] { 0x55, 0x30, 0x01, 0x01, 0x01, 0x03, 0x04, 0x00, 0x04, 0x93, 0x11, 0xD0, 0x60, 0x03, 0xFA };

                    //TramaTemporal = new byte[15] { 0x55, 0x30, 0x01, 0x01, 0x02, 0x03, 0x04, 0x00, 0x04, 0x93, 0x11, 0xE3, 0x60, 0x03, 0xFA };


                    //TramaTemporal = new byte[25] { 0x53, 0x30, 0x01, 0x01, 0x05, 0x03, 0x04, 0x00, 0x04, 0x97, 0x01, 0x02, 0x08, 0x00, 0x00, 0x24, 0x14, 0x00, 0x01, 0x20, 0x00, 0x95, 0xEE, 0x03, 0xFA };
                    //TramaTemporal = new byte[25] { 0x55, 0x30, 0x03, 0x04, 0x00, 0x04, 0x93, 0x01, 0x02, 0x08, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x14, 0x01, 0x01, 0x05, 0xAB, 0xC5, 0x03, 0xFA };

                    //TramaTemporal = new byte[11] { 0x51, 0x30, 0x05, 0x03, 0x00, 0x05, 0x25, 0x4B, 0xFA, 0x03, 0xFA};

                    //|2|Tx|EstalecerPrecio|51|30|05|03|00|05|25|4B|FA|03|FA|


                    //TramaTemporal = new byte[25] { 0x55, 0x30, 0x65, 0x10, 0x01, 0x01, 0x65, 0x87, 0x85, 0x25, 0x01, 0x65, 0x87, 0x85, 0x25, 0x00, 0x00, 0x00, 0x00, 0x00, 0x35, 0x10, 0xFA, 0x03, 0xFA };

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco


                    //nuevo firware
                    //TramaTemporal = new byte[3] { 0x54, 0x70, 0xFA };

                    //TramaTemporal = new byte[3] { 0x54, 0xC0, 0xFA };

                    //TramaTemporal = new byte[15] { 0x54, 0x31, 0x01, 0x01, 0x06, 0x03, 0x04, 0x00, 0x04, 0x65, 0x01, 0xB5, 0xF5, 0x03, 0xFA };
                    
                    
                    PuertoCom.Read(TramaTemporal, 0, Bytes);
                    PuertoCom.DiscardInBuffer();


                    //Se dimensiona la Trama a evaluarse (TramaRx)
                    TramaRx = new byte[TramaTemporal.Length];
                    string strTrama = "";
                    CRC_TramaRx = "";
                    //Almacena los datos reales (sin eco) en TramaRx
                    for (int i = 0; i < (TramaTemporal.Length); i++)
                    {
                        TramaRx[i] = TramaTemporal[i];
                        strTrama += TramaRx[i].ToString("X2") + "|";

                        if (TramaTemporal.Length > 3)//si la Trama tiene mas de 3 bytes tendra CRC y se realizasa su analisis.
                        {
                            //Si el crc RECIVIDO TIENE 0XFA SE DEBE COPIAR LA TRAMA ANTE DE DEL 10 O EL CRC 0
                            if (TramaTemporal[TramaTemporal.Length - 3] == 0xFA | TramaTemporal[TramaTemporal.Length - 4] == 0xFA)
                            {
                                if (i < TramaRx.Length - 5)
                                    CRC_TramaRx += TramaRx[i].ToString("X2");
                                //Where CRC value have byte (FA Hex)  put behind  CRC value  10Hex  
                                //Egzample 1 ; CRC=1E FA  sended byte will be 1E 10 FA  
                                //Egzample 2 ; CRC= FA 78 sended byte will be 10 FA 78  
                            }
                            else // COPIA TODO LOS DATOS MENOS CUATRO ELEMENTOS CRC1 CRC2 ETX Y SF
                            {
                                if (i < TramaRx.Length - 4)
                                    CRC_TramaRx += TramaRx[i].ToString("X2");
                            }
                        }
                    }

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                         "|" + CaraEncuestada + "|Rx|" + ComandoCaras + "|" + strTrama);
                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////


                    //Revisa si existe problemas en la trama
                    if (ComprobarIntegridadTrama())
                    {

                        FalloComunicacion = false; //DCF 20110117
                        //if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2ACK)
                        if (ComandoCaras != ComandoSurtidor.Pool)
                        {
                            AnalizarTrama();
                        }
                    }

                    else
                    {
                        FalloComunicacion = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Comando " + ComandoCaras + ". Bytes con fallo en integridad de trama");
                        SWRegistro.Flush();
                    }
                }
                else if (FalloComunicacion == false)
                {
                    FalloComunicacion = true;
                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|" + ComandoCaras + "  " + " - Bytes Recibidos: " + Bytes);
                        SWRegistro.Flush();
                    }
                }


                //Thread.Sleep(50); //tiempo de espera para escribir sobre el surtidor 50 ms sugerencia del fabricante
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|RecibirInformacion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public bool ComprobarIntegridadTrama()
        {
            try
            {
                if (TramaRx.Length > 3) // es un Data
                {
                    // Comando enviado TNO = TNO recivido byte 2 //
                    if ((TNO != TramaRx[2]) && (TNO != 1) && (TNO != 0) && ComandoCaras != ComandoSurtidor.Pool)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|TNO recivido Errado" + ComandoCaras);
                        SWRegistro.Flush();
                        return false;
                    }

                    ////


                    //Calcular CRC
                    //string strTramaRx = Encoding.Default.GetString(TramaRx, 0, TramaRx.Length-4);
                    byte[] CRCCalculado = CalcularCRC(CRC_TramaRx);


                    if (((CRCCalculado[0] == TramaRx[TramaRx.Length - 4] && CRCCalculado[1] == TramaRx[TramaRx.Length - 3]) | (PropiedadesCara[CaraEncuestada].CRC_FA))
                        && 0x03 == TramaRx[TramaRx.Length - 2]
                         && 0xfa == TramaRx[TramaRx.Length - 1])
                    {

                        ComandoCaras = ComandoSurtidor.Data;//Se recivio un DATA

                        return true;
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|CRC Errado en comando " + ComandoCaras);
                        SWRegistro.Flush();

                        return false;
                    }

                }

                else
                {
                    if (TramaRx.Length == 3) // ACK confirmacion de 
                    {
                        //                             ADR                           ACK                    FA
                        if (TramaRx[0] == (0x50 + (CaraEncuestada - 1)) && TramaRx[1] == 0xC0 && TramaRx[2] == 0xFA)
                        {

                            CTRL = 0x02; // Poll  para obtener el dato enviado antes de la respuesta "ACK"


                            //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2ACK;

                            //if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Descolgada) //No funciona para obtener totales???
                            {
                                ProcesoEnvioComando(ComandoSurtidor.Pool); // en tomar accion
                                ComandoCaras = ComandoSurtidor.Pool;
                            }
                        }

                        //                          ADR                           EOT                    FA
                        if (TramaRx[0] == (0x50 + (CaraEncuestada - 1)) && TramaRx[1] == 0x70 && TramaRx[2] == 0xFA)
                        {
                            CTRL = 0x03; // Poll  para obtener el dato enviado antes de la respuesta "ACK"


                            ProcesoEnvioComando(ComandoSurtidor.Estado); // en tomar accion OK

                            //ProcesoEnvioComando(ComandoSurtidor.Reset); // en tomar accion OK


                        }



                        //                          ADR                           NAK                    FA
                        if (TramaRx[0] == (0x50 + (CaraEncuestada - 1)) && TramaRx[1] == 0x50 && TramaRx[2] == 0xFA)
                        {
                            return false;
                        }

                        return true;


                    }

                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|TramaRx " + ComandoCaras);
                        SWRegistro.Flush();
                        return false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Método ComprobarIntegridadTrama: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }


        #endregion

        #region ANALISIS DE TRAMAS Y RECONSTRUCCIÓN DE DATOS PROVENIENTE DEL SURTIDOR

        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        public void AnalizarTrama()
        {
            try
            {
                //  ****************************  ****************************
                // **************************** Recupera datos para 2A ****************************
                posicion_byte = 0;

                ADR = TramaRx[0];
                posicion_byte += 1;

                CTRL = TramaRx[1];
                posicion_byte += 1;

                //while (posicion_byte < (TramaRx.Length - 4))
                //{
                //    Recuperar_DatosRX();
                //}

                //DCF 22/082011
                if (!PropiedadesCara[CaraEncuestada].CRC_FA)
                {
                    while (posicion_byte < (TramaRx.Length - 4))
                    {
                        Recuperar_DatosRX();
                    }

                }
                else
                {
                    while (posicion_byte < (TramaRx.Length - 5))
                    {
                        Recuperar_DatosRX();
                    }

                }

                //DCF 22/082011


                //  ****************************  ****************************



                // ****************************  ****************************
                // Asignacion de Estado Consolidado  se termina el recorrido por el RX aqui asignar estado:
                // ****************************  ****************************
                //cambio1
                if (PropiedadesCara[CaraEncuestada].Manguera_ON == false)
                {
                    ////Almacena en archivo el estado actual del surtidor
                    ////Almacena en archivo el estado actual del surtidor
                    if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                        PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                    if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.A2Reposo)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|A2Reposo ");
                        SWRegistro.Flush();
                    }

                    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Reposo;

                    ////Nuevo DCF EsVentaParcial = true en manguera colgada. //03-08-2011 No funciono revisar
                    //if (PropiedadesCara[CaraEncuestada].EsVentaParcial == true)
                    //{
                    //    //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.FinDespachoForzado;
                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|Finaliza la venta en Estado Espera -- EsVentaParcial = true ");
                    //    SWRegistro.Flush();


                    //    ProcesoFindeVenta();
                    //    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                    //}                   

                }
                else
                {
                    if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Despacho &&
                        PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Autorizado &&
                        PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2DespachoAutorizado &&
                        PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2FinDespacho &&
                        PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Predeterminacio_Alcanzada)
                    {
                        ////Almacena en archivo el estado actual del surtidor
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.A2Descolgada)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|A2Descolgada ");
                            SWRegistro.Flush();
                        }





                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Descolgada;
                    }


                }


                //



                switch (ComandoCaras) // 
                {

                    case ComandoSurtidor.Data:

                        //if ((PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.A2FinDespacho) &&
                        //(PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Reset))

                        if (PropiedadesCara[CaraEncuestada].VentaTermina)
                        {
                            ProcesoEnvioComando(ComandoSurtidor.Reset); // se debe enviar el comando reset luego de que el surtidor reporte fin de venta 0x05

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Envio de Rest en VentaTermina..");
                            SWRegistro.Flush();
                        }

                        //if ((PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.A2Reset) &
                        //    (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Reset) & PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Despacho) //& PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Reposo) //REGRESO DC03-DC02 -DC01 RESET

                        if (PropiedadesCara[CaraEncuestada].Enviar_ACK)
                        {

                            ProcesoEnvioComando(ComandoSurtidor.Ack); //revisar??? ack activado y luego desactivar

                            PropiedadesCara[CaraEncuestada].Enviar_ACK = false;

                        }
                        break;



                    case ComandoSurtidor.Estado:
                        RecuperarEstado();
                        break;



                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now.Second + "|" + CaraEncuestada + "|Excepcion|AnalizarTrama: " + " | ComandoCaras :" + ComandoCaras + " | " + Excepcion);
                SWRegistro.Flush();



                var lineNumber = new System.Diagnostics.StackTrace(Excepcion, true).GetFrame(0).GetFileLineNumber();

                SWRegistro.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                 DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                 DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') + "  --- Line: " + lineNumber);

                SWRegistro.Flush();
            }
        }


        public void Recuperar_DatosRX()
        {
            try
            {

                TNO = TramaRx[posicion_byte];
                posicion_byte += 1;

                LNG = TramaRx[posicion_byte]; //sumar el numero del leng ??
                posicion_byte += 1;

                Datablok = new byte[LNG];

                for (int i = 0; i < LNG; i++)
                {
                    Datablok[i] = TramaRx[posicion_byte];
                    posicion_byte += 1;


                    if (posicion_byte > TramaRx.Length - 4)
                    {
                        break;
                    }
                }


                switch (TNO) //Transaccion 
                {
                    case 0X01:
                        #region PUMP STATUS DC01

                        //if (Datablok[0] == 1) // Estado reset para no altera estado Anteriro
                        //{
                        //    PropiedadesCara[CaraEncuestada].VentaTermina = false;
                        //    break;
                        //}

                        RecuperarEstado(); //recupera el estado entregado por el surtidor
                        PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK 
                        break;
                        #endregion

                    case 0x02:
                        #region   DC2: VOLUMEN DE LLENADO Y CANTIDAD-- es retornado con CD1 DCC= 4h
                        VOLVenta = "";
                        Importe = "";
                        //TramaRx.Length-1 Convert.ToInt16(TramaRx[3]) = tamaño del dato LNG
                        for (int i = 0; i < (Datablok.Length) - 4; i++)
                        {
                            VOLVenta += (Datablok[i].ToString("X2"));
                        }

                        for (int i = 4; i < (Datablok.Length); i++)
                        {
                            Importe += (Datablok[i].ToString("X2"));
                        }


                        // *********** Se obtiene el Volumen de la venta anterior ******************
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta =
                            Convert.ToDecimal(VOLVenta) / PropiedadesCara[CaraEncuestada].FactorVolumen;
                        // *********** *********** ****************** *********** *********** *******

                        // *********** Se obtiene el Importe de la venta anterior ******************
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte =
                            Convert.ToDecimal(Importe) / PropiedadesCara[CaraEncuestada].FactorImporte;
                        // *********** *********** ****************** *********** *********** *******   


                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inspección|FactorVolumen =  " + PropiedadesCara[CaraEncuestada].FactorVolumen);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******
                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inspección|FactorPrecio =  " + PropiedadesCara[CaraEncuestada].FactorPrecio);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******


                        if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2Despacho) //PARCIALES DE VENTA
                        {
                            string strTotalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte.ToString("N3");
                            string strVolumen = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta.ToString("N3");
                            if (VentaParcial != null)
                            {
                                VentaParcial(CaraEncuestada, strTotalVenta, strVolumen);
                            }

                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|InformarVentaParcial: " + "Cara: " + CaraEncuestada + " |Importe: " + strTotalVenta + " |Volumen: " + strVolumen);
                            //SWRegistro.Flush(); // Borra solo para Inspección DCF *******

                        }


                        PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK 

                        break;
                        #endregion;

                    case 0x03:
                        #region   DC3:  ESTADO DE BOQUILLA Y PRECIOS DE LLENADO
                        {
                            PrecioVenta = "";

                            for (int i = 0; i <= (Datablok.Length - 2); i++) //Precio de lledado
                            {
                                
                                
                                PrecioVenta += (Datablok[i].ToString("X2")); //Precio de Venta entregado por la cara del surtidor. OK
                                ////PrecioVenta += Convert.ToString(Convert.ToInt32(Datablok[i]), 16); //Precio de Venta entregado por la cara del surtidor.

                                
                            }
                        

                            //// se quita esta opción de detectar el grado ya que en algunos surtidores regresan el grado diferente aun teniendo una sola manguera 
                            //// Manguera encuestada 
                            //PropiedadesCara[CaraEncuestada].GradoCara = Convert.ToInt16((0X0F & Datablok[3]) - 1); // bits 0-3 Manguera selecionada

                            PropiedadesCara[CaraEncuestada].GradoCara = 0x00; //por defecto una sola manguera por lado  2011.11.04-1015"                          

                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 =
                           (Convert.ToDecimal(PrecioVenta) / (PropiedadesCara[CaraEncuestada].FactorPrecio));


                            //int INOUTBoquilla = Convert.ToInt16(0x10 & Datablok[3]); // 0= in/ 1 = Out
                            if (Convert.ToInt16(0x10 & Datablok[3]) == 0x10)// 0= in/ 1 = Out Estado de la Manguera
                            {

                                ////Almacena en archivo el estado actual del surtidor
                                ////if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                                ////    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                                //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Descolgada;

                                PropiedadesCara[CaraEncuestada].Manguera_ON = true; //activo bandera para asignar estado al finalizar el recorido por el RX 

                                //Obtener Grado de la cara                               
                                PropiedadesCara[CaraEncuestada].GradoVenta = PropiedadesCara[CaraEncuestada].GradoCara; //Asignacion del GradoCara que esta por despachar 
                            }

                            else
                            {

                                PropiedadesCara[CaraEncuestada].Manguera_ON = false; //Manguera Off


                                //Cambio en el fin de venta 13-06-2013 DCF 

                                if (PropiedadesCara[CaraEncuestada].ObtenerDatos_Ventas)
                                {
                                    if (PropiedadesCara[CaraEncuestada].DespachoA2)// PREGUNTAR SI TAMBIEN FUE AUTORIZADA PERO NO DESPACHO
                                    {
                                        PropiedadesCara[CaraEncuestada].DespachoA2 = false;
                                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2FinDespacho;

                                        //PropiedadesCara[CaraEncuestada].FinDespachoA2 = true;

                                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                                        {
                                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ProcesoFindeVenta en FILLlNG COMPLETED|");
                                            SWRegistro.Flush();

                                            ProcesoFindeVenta();
                                        }
                                    }
                                    PropiedadesCara[CaraEncuestada].VentaTermina = true;

                                    PropiedadesCara[CaraEncuestada].ObtenerDatos_Ventas = false;
                                }
                            }


                            PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK// Por recomendacion 17/06/2011
                        }

                        break;
                        #endregion;


                    case 0x65:
                        #region    DC101:   VOLUME TOTAL COUNTERS (*) 65h
                        //DC65.Text = "DC65 OK";

                        int contaBoquilla = 0;
                        VOLTotal = "";
                        VOLTotal1 = "";
                        VOLTotal2 = "";

                        //PropiedadesCara[CaraEncuestada].GradoCara = (Convert.ToInt16(Datablok[0]) - 1);
                        PropiedadesCara[CaraEncuestada].GradoCara = 0x00;

                        contaBoquilla = Convert.ToInt16(Datablok[0].ToString("X2")); //Volumen total de número de contador (1 = boquilla 1, 2 = boquilla 2, ...)

                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Numero de Boquillas en Cara: " + contaBoquilla);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******


                        //DCF para EDE sinlanza Peru RC ingeniero 26/07/2017
                        if (Datablok.Length == 0x06 )
                        {

                            for (int i = 1; i < (Datablok.Length); i++) //El volumen total de la boquilla 5 bytes
                            {
                                VOLTotal1 += (Datablok[i].ToString("X2")); //Convert.ToInt16(TramaRx[i]);
                            }

                        }
                        else 
                        {

                            //TramaRx.Length-1 Convert.ToInt16(TramaRx[3]) = tamaño del dato LNG
                            for (int i = 1; i < (Datablok.Length) - 10; i++) //El volumen total de la boquilla 5 bytes
                            {
                                VOLTotal += (Datablok[i].ToString("X2")); //
                            }

                            for (int i = 6; i < (Datablok.Length) - 5; i++) //El volumen total de la boquilla 5 bytes
                            {
                                VOLTotal1 += (Datablok[i].ToString("X2")); //Convert.ToInt16(TramaRx[i]);
                            }

                            for (int i = 11; i < (Datablok.Length); i++) //El volumen total de la boquilla 5 bytes
                            {
                                VOLTotal2 += (Datablok[i].ToString("X2")); // Convert.ToInt16(TramaRx[i]);
                            }
                        }


                        if (PropiedadesCara[CaraEncuestada].GradoCara == 0x00) // Toma el totalizador de Volumen para el Grado 1 
                        {
                            //LecturaFinalVenta para el totalizador
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorVolumen =
                                Convert.ToDecimal(VOLTotal1) / PropiedadesCara[CaraEncuestada].FactorTotalizador;
                        }

                        if (PropiedadesCara[CaraEncuestada].GradoCara == 0x01) // Toma el totalizador de Volumen para el Grado 2
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorVolumen =
                                Convert.ToDecimal(VOLTotal1) / PropiedadesCara[CaraEncuestada].FactorTotalizador;
                        }


                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inspección|FactorTotalizador =  " + PropiedadesCara[CaraEncuestada].FactorTotalizador);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******

                        PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK 

                        break;
                        #endregion;


                    case 0x66:
                        #region    DC102:  AMOUNT TOTAL COUNTERS (*) 0x66 h

                        //DC66.Text = "DC66 OK";

                        contaBoquilla = 0;
                        ImpTotal = "";
                        ImpTotal1 = "";
                        ImpTotal2 = "";

                        //PropiedadesCara[CaraEncuestada].GradoCara = (Convert.ToInt16(Datablok[0]) - 1);//esto se controla por la configuracion en el surtidor
                        PropiedadesCara[CaraEncuestada].GradoCara = 0x00;
                        contaBoquilla = Convert.ToInt16(Datablok[0].ToString("X2")); //Volumen total de número de contador (1 = boquilla 1, 2 = boquilla 2, ...)

                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Numero de Boquillas en Cara: " + contaBoquilla);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******



                        //DCF para EDE sinlanza Peru RC ingeniero 26/07/2017
                        if (Datablok.Length == 0x06)
                        {

                            for (int i = 1; i < (Datablok.Length); i++) //El volumen total de la boquilla 5 bytes
                            {
                                ImpTotal1 += (Datablok[i].ToString("X2")); //Convert.ToInt16(TramaRx[i]);
                            }

                        }
                        else
                        {

                            for (int i = 1; i < (Datablok.Length) - 10; i++) //El volumen total de la boquilla 5 bytes
                            {
                                ImpTotal += (Datablok[i].ToString("X2")); //Convert.ToInt16(TramaRx[i]);
                            }

                            for (int i = 6; i < (Datablok.Length) - 5; i++) //El volumen total de la boquilla 5 bytes
                            {
                                ImpTotal1 += (Datablok[i].ToString("X2")); //Convert.ToInt16(TramaRx[i]);
                            }

                            for (int i = 11; i < (Datablok.Length); i++) //El volumen total de la boquilla 5 bytes
                            {
                                ImpTotal2 += Datablok[i].ToString("X2"); //Convert.ToInt16(TramaRx[i]);
                            }

                        }

                        if (PropiedadesCara[CaraEncuestada].GradoCara == 0x00) // Toma el totalizador de Importe para el Grado 1 
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorImporte = Convert.ToDecimal(ImpTotal1) / PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte;

                        }

                        if (PropiedadesCara[CaraEncuestada].GradoCara == 0x01) // Toma el totalizador de Importe para el Grado 2 
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorImporte = Convert.ToDecimal(ImpTotal2) / PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte;

                        }


                        //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|" + "Grado=" + PropiedadesCara[CaraEncuestada].GradoCara + "| Totalizador de Importe: " +
                        //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorImporte);
                        //SWRegistro.Flush(); // Borra solo para Inspección DCF *******



                        PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK 

                        break;
                        #endregion;

                    case 0x05:
                        #region DC5: CODIGO DE ALARMA  (*)

                        switch (Convert.ToByte(Datablok[0])) // LNG= 1;                      
                        {
                            //1 CPU restablecer
                            //3 RAM de error
                            //4 PROM error de comprobación
                            //6 Pulsador de error
                            //7 Pulsador actual de error
                            //9 Parada de emergencia
                            //A. Falla de energía
                            //B. Perdido de Presión 
                            //C. mezcla de error de relación
                            //D. Baja Fugas de Error 
                            //E. Alta fuga de error

                            case 0x01:
                                //DC05.Text = "CPU restablecer";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 1| Restablecer CPU");
                                SWRegistro.Flush(); // 

                                break;
                            case 0x03:
                                //DC05.Text = "RAM de error";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 3| Error de RAM");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x04:
                                //DC05.Text = "PROM Error de Checksum;
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 4| Error de Checksum");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x06:
                                //DC05.Text = "Pulsador de error";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 6| Error de Pulsador");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x07:
                                //DC05.Text = "Pulsador actual de error";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 7| Error actual en generador de impulsos ");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x09:
                                //DC05.Text = "Parada de emergencia";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma 9| Parada de emergencia");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x0A:
                                //DC05.Text = " Falla de energía";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma A| Falla de energía");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x0B:
                                //DC05.Text = "Perdido de Presión";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma B| Perdido de Presión");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x0C:
                                //DC05.Text = "Mezcla de error de relación";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma C| Error de RAM");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x0D:
                                //DC05.Text = "Error de Baja Fugas";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma| Error relación de Mezcla");
                                SWRegistro.Flush(); // 
                                break;
                            case 0x0E:
                                //DC05.Text = "Error de Alta fuga";
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Alarma| Error de Alta fuga");
                                SWRegistro.Flush(); //                  
                                break;

                        }

                        PropiedadesCara[CaraEncuestada].Enviar_ACK = true; //Avilito el envio del ACK 



                        break;

                        #endregion


                    case 0x14:
                        #region   DC14: RESPUESTA SUSPENDER (*)

                        //DC14.Text = "Boquilla Stop: " + Datablok[0].ToString("X2");

                        break;


                        #endregion;


                    case 0x15:
                        #region   CD15: SOLICITUD DE REANUDAR

                        //DC15.Text = "Boquilla Play: " + Datablok[0].ToString("X2");

                        break;


                        #endregion;


                    case 0x67:

                        //DC67.Text = "DC67 OK";
                        break;
                    case 0x68:

                        //DC68.Text = "DC68 OK";
                        break;
                    case 0x07:

                        //DC07.Text = "DC07 OK";
                        break;

                    case 0x09:

                        //DC09.Text = "DC09 OK";
                        break;
                }


                //al finalizar el recorrido por el RX se asignara el estado.




            }
            catch (Exception)
            {

                throw;
            }


        } //OK






        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        public void RecuperarEstado()
        {
            try
            {
                ////Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;


                switch (Convert.ToByte(Datablok[0])) // LNG= 1;
                {
                    //Recuperar estado y visualizar:             
                    //- 0h 	PUMP NOT PROGRAMMED 
                    //- 1h 	RESET 
                    //- 2h 	AUTHORIZED 
                    //- 4h 	FILLING 
                    //- 5h 	FILLlNG COMPLETED 
                    //- 6h 	MAX AMOUNT / VOLUME REACHED 
                    //- 7h 	SWlTCHED OFF 
                    case 0:
                        //DC01.Text = "PUMP NOT PROGRAMMED";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2PumpNotProgramada;
                        break;

                    case 1:
                        //DC01.Text = "RESET ";
                        //PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Reset;

                        PropiedadesCara[CaraEncuestada].VentaTermina = false;
                        break;

                    case 2:
                        //DC01.Text = "AUTHORIZED";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Autorizado;
                        break;

                    case 4:
                        //DC01.Text = "FILLING ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Despacho;
                        PropiedadesCara[CaraEncuestada].DespachoA2 = true;
                        break;

                    case 5:
                        //DC01.Text = "FILLlNG COMPLETED";

                        //////////////////////////if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.A2Despacho)
                        //////////////////////////PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2FinDespacho;

                        //if (PropiedadesCara[CaraEncuestada].DespachoA2)// PREGUNTAR SI TAMBIEN FUE AUTORIZADA PERO NO DESPACHO
                        //{
                        //    PropiedadesCara[CaraEncuestada].DespachoA2 = false;
                        //    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2FinDespacho;

                        //    //PropiedadesCara[CaraEncuestada].FinDespachoA2 = true;

                        //    if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        //    {
                        //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ProcesoFindeVenta en FILLlNG COMPLETED|");
                        //        SWRegistro.Flush();

                        //        ProcesoFindeVenta();
                        //    }
                        //}
                        //PropiedadesCara[CaraEncuestada].VentaTermina = true;


                        PropiedadesCara[CaraEncuestada].ObtenerDatos_Ventas = true;

                        break;


                    case 6:
                        //DC01.Text = "MAX AMOUNT/VOLUME REACHED";
                       // PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Predeterminacio_Alcanzada; // se quita este estado no se utiliza 

                        //enviar el proceso fin de venta realizar pruebas en lab 27/05/2013
                         //*************************************


                        //if (PropiedadesCara[CaraEncuestada].DespachoA2)// PREGUNTAR SI TAMBIEN FUE AUTORIZADA PERO NO DESPACHO
                        //{
                        //    PropiedadesCara[CaraEncuestada].DespachoA2 = false;
                        //    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2FinDespacho;

                        //    //PropiedadesCara[CaraEncuestada].FinDespachoA2 = true;

                        //    if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        //    {
                        //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ProcesoFindeVenta en Predeterminacio_Alcanzada.|");
                        //        SWRegistro.Flush();

                        //        ProcesoFindeVenta();
                        //    }
                        //}

                        //PropiedadesCara[CaraEncuestada].VentaTermina = true; //21/05/2013 se envia el reset luego de presentarse este estado "A2Predeterminacio_Alcanzada"
                        
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|se envia stop .... para finalizar la venta|");
                        SWRegistro.Flush(); //05-06-2013 dcf

                        ProcesoEnvioComando(ComandoSurtidor.stop);//05-06-2013 dcf ok                       

                        break;

                    case 7:
                        //DC01.Text = "SWlTCHED OFF ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.A2Switched_OFF;
                        break;
                }

                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado.|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                    SWRegistro.Flush();
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado.|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                SWRegistro.Flush();//Borra solo para prue...

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|Recucepcion  " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region PROCESOS DE TOMA DE DECISIONES SEGÚN ESTADOS DE LA CARA

        //DEPEEN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        public void TomarAccion()
        {
            try
            {
                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    case (EstadoCara.A2ACK): // Respuesta del surtidor positiva, acepto el comando enviado.
                        ProcesoEnvioComando(ComandoSurtidor.Pool); //envio de Poll para que responda el comndo enviado 

                        break;

                    case (EstadoCara.A2Reposo):
                        #region A2Reposo

                        //Informa cambio de estado
                        //if ((PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado) & (PropiedadesCara[CaraEncuestada].Manguera_ON))

                        if ((PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado) & (!PropiedadesCara[CaraEncuestada].Manguera_ON))
                        {
                            //if (PropiedadesCara[CaraEncuestada].VentaTermina) //ojo comprobar
                            if (!PropiedadesCara[CaraEncuestada].EsVentaParcial)//05-06-2013 dcf control de envio InformarCaraEnReposo
                            {
                                int IdManguera = 0;
                                IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                                //PropiedadesCara[CaraEncuestada].Manguera_ON = false; //Desactivar cuando se detecte manguera abajo

                                if (CaraEnReposo != null)
                                {
                                    CaraEnReposo(CaraEncuestada, IdManguera);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera. Grado " + IdManguera);
                                SWRegistro.Flush();
                            }
                        }
                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();


                        //*******************************************************************************************************************
                        //*******************************************************************************************************************    
                        //en caso de  cerrar el encuestador en medio de una venta se debe colocar a false el EsVentaParcial DCF 03/08/2011
                        //if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        //*** Recuperar venta para 2A cambio de estados Despacho - Reposo ***** 2013/12/13 - 0814
                        if(PropiedadesCara[CaraEncuestada].RecuperarVenta)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento| VentaParcial True y envio ProcesoFindeVenta ***");
                            SWRegistro.Flush();
                            ProcesoFindeVenta();

                            PropiedadesCara[CaraEncuestada].RecuperarVenta = false; //se desactiva la recuperación de ventas luego de un reinicio con una venta en curso.
                            
                        }
                                 


                        break;
                        #endregion;

                    //case (EstadoCara.A2Reset): //si esta en reset y descolgada se pide la autorizacion 
                    case (EstadoCara.A2Descolgada):
                        #region A2Descolgada

                        //if ((PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.A2Descolgada) ||
                        //    (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2Descolgada)) //Estado OK --EstadoAnterior =Reset
                        {

                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.A2Despacho || PropiedadesCara[CaraEncuestada].EstadoAnterior == EstadoCara.A2Autorizado)
                            {
                                break; // ignore y salte 
                            }



                            //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error
                            if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                            {
                                string MensajeErrorLectura = "Manguera descolgada";
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                                {
                                    bool EstadoTurno = false;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    }
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                                {
                                    bool EstadoTurno = true;
                                    PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                    if (CancelarProcesarTurno != null)
                                    {
                                        CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                    }
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                    SWRegistro.Flush();
                                }
                                //Se establece valor de la variable para que indique que ya fue reportado el error
                                PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                            }

                            //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                                PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                            {
                                //PropiedadesCara[CaraEncuestada].Manguera_ON = true; //Reset la bandera para no entrar hasta se cuelgue la manguera 

                                PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta;
                                //if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio)) //Ojo ya lo obtengo en el estado DC3 y el estado tambien lo obtengo 
                                //{

                                if (ProcesoTomaLectura()) // Toma de totalizador de volumen antes de iniciar la venta 
                                {
                                    int IdProducto =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto;
                                    int IdManguera =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;
                                    string Lectura =
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorVolumen.ToString("N2");
                                    //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta.ToString("N2");

                                    // ***************************** **********************************
                                    //se almacena el totalizador de volumen en TotalizadorVolumen_Inicial
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorVolumen;
                                    // ***************************** **********************************

                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorVolumen = 0;
                                    if (AutorizacionRequerida != null)
                                    {
                                        AutorizacionRequerida(CaraEncuestada, IdProducto, IdManguera, Lectura,"");
                                    }
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                        + PropiedadesCara[CaraEncuestada].GradoVenta + " - Producto: " +
                                        IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura + " - Precio: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1);
                                    SWRegistro.Flush();

                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando de obtener Totalizador de Volumen para Lectura Inicial Venta");
                                    SWRegistro.Flush();
                                }
                                                               

                                if (ProcesoTomaLecturaImporte()) //Toma de Totalizadores de importe
                                {
                                    string lecturaImporte =
                                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorImporte.ToString("N2");

                                    // ***************************** **********************************
                                    //se almacena el totalizador de TotalizadorImporte_Iniciall
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].TotalizadorImporte;
                                    // ***************************** **********************************

                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte = 0;

                                }

                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando de TomaLecturaImporte");
                                    SWRegistro.Flush();
                                }


                            }
                            //Revisa en el vector de Autorizacion si la venta se debe autorizar
                            if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                            {

                                //Enviar reset recomendacion del fabricante
                                ProcesoEnvioComando(ComandoSurtidor.Reset); // se debe enviar el comando reset antes de autorizar 11-06-2013.
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Envio de Rest antes de Autorizar");
                                SWRegistro.Flush();

                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                     PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial;
                                //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta;

                                string strLecturasVolumen = "0";
                                    strLecturasVolumen=PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N2");
                                if (LecturaInicialVenta != null)
                                {
                                    LecturaInicialVenta(CaraEncuestada, strLecturasVolumen);
                                }
                                //Loguea Evento de envio de lectura
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informar Lectura Inicial de Venta: " +
                                    strLecturasVolumen);
                                SWRegistro.Flush();

                                //lectura inicial de importe para el calculo final de la venta DCF
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial;

                                string strLecturasImporte = "0";
                                strLecturasImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialImporte.ToString("N2");

                                //Loguea Evento de envio de lectura
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informar Lectura Inicial de Importe: " + strLecturasImporte);
                                SWRegistro.Flush();
                                //*********************************** DCF


                                if (PropiedadesCara[CaraEncuestada].PredeterminarVolumen)
                                {
                                    ProcesoEnvioComando(ComandoSurtidor.Predeterminar_Volumen);

                                }


                                //Valor de Predeterminacion en $$
                                if (PropiedadesCara[CaraEncuestada].PredeterminarImporte)
                                {
                                    ProcesoEnvioComando(ComandoSurtidor.Predeterminar_Importe);
                                    //{
                                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Predeterminacion exitosa. Importe: " +
                                    //        PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                                    //    SWRegistro.Flush();
                                    //}
                                    //else
                                    //{
                                    //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Proceso de predetermiancion fallido");
                                    //    SWRegistro.Flush();
                                    //}
                                }



                                int Reintenos = 1;
                                do
                                {
                                    //if (!ProcesoEnvioComando(ComandoSurtidor.Autorizar)) //Autorizacion

                                    ProcesoEnvioComando(ComandoSurtidor.Autorizar);//ENVIO DE AUTORIZACION 

                                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|EStado despues de Autorizar" + PropiedadesCara[CaraEncuestada].Estado);
                                    //SWRegistro.Flush(); //Borrar solo prueba 

                                    if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Despacho && PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Autorizado)
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondió comando de Autorizar Despacho");
                                        SWRegistro.Flush();



                                    }
                                    else
                                    {
                                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Autorizacion enviado con exito");
                                        SWRegistro.Flush();

                                        PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                                        break;

                                    }

                                    ProcesoEnvioComando(ComandoSurtidor.Estado);
                                    Reintenos++;
                                } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Autorizado &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Reposo &&
                                    PropiedadesCara[CaraEncuestada].Estado != EstadoCara.A2Despacho &&
                                    Reintenos <= 2); //DCF WaynePredeterminada 15-03-11 WayneFinDespacho_AF
                            }


                            //DCF 15-03-11 ********************************************
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2Predeterminada) // envio de mensaje si se predetermina con manguera levantada 15-03-11
                            {
                                string caraError = Convert.ToString(CaraEncuestada);

                                string Mensaje = "Error al Predeterminar. Por Favor Cuelgue la Manguera, predetermine el valor antes de levantar la manguera para realizar la venta satisfactoriamente en la cara: ";
                                Mensaje = Mensaje + caraError;
                                bool Imprime = true;
                                bool Terminal = false;
                                string puerto = "COM1";

                                if (ExcepcionOcurrida != null)
                                {
                                    ExcepcionOcurrida(Mensaje, Imprime, Terminal, puerto);
                                }

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "| Impresion de Error al Predeterminar con Manguera Descolgada");
                                SWRegistro.Flush();

                            }
                            //DCF 15-03-11 ********************************************

                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2DespachoAutorizado ||
                                PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2Despacho)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Autorizacion Aceptado");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = true;//OK

                                //// grabar el grado que inicio la venta DCF 04-07-11
                                PropiedadesCara[CaraEncuestada].GradoVentaInicial = PropiedadesCara[CaraEncuestada].GradoVenta;



                            }
                            else if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.A2Reposo)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Manguera colgada luego de autorizada");
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].AutorizarCara = false;
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                                if (VentaInterrumpidaEnCero != null)
                                {
                                    VentaInterrumpidaEnCero(CaraEncuestada);
                                }

                            }

                            //Activar Bandera para precaucion en fin de Venta DCF 16-03-11 ********************************************
                            if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.WaynePredeterminada && PropiedadesCara[CaraEncuestada].AutorizarCara == false)
                            {
                                PropiedadesCara[CaraEncuestada].PosibleErrorFinVenta = true;
                            }
                            else
                            {
                                PropiedadesCara[CaraEncuestada].PosibleErrorFinVenta = false;
                            }
                            //****************************************************************************************



                        }

                        break;

                        #endregion;

                    case EstadoCara.A2Autorizado:
                        #region Autorizado
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Autorizada";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        /*   el surtidor envio estado Autorizado pero no inicio el despacho por tal motivo se elimina estas condiciones
                      
                         * //Reset del elemento que indica que la Cara debe ser autorizada
                           if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                               PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                           //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                           if (PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                               PropiedadesCara[CaraEncuestada].EsVentaParcial = true; //??
                            */
                        break;
                        #endregion;

                    case EstadoCara.A2Despacho:
                        #region Despacho

                        if (PropiedadesCara[CaraEncuestada].DetenerVentaCara) //DCF probar en EDS detenido por Monitoreo de chip
                        {
                            ProcesoEnvioComando(ComandoSurtidor.SuspenderLlenado);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Detención de Venta Por Monitoreo de Chip ");
                            SWRegistro.Flush();

                        } //revisar que estado regresa cuando se envia este comado ????DCF 


                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Despacho";
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraEncuestada, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                            PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true; //OK

                        //Reporta los valores de parciales de despacho         
                        if (PropiedadesCara[CaraEncuestada].TotalVenta != 0 & PropiedadesCara[CaraEncuestada].Volumen != 0)// si tienen datos los envia para su visualizacion 06/03/2013
                        {
                            string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                            string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                            if (VentaParcial != null)
                            {
                                VentaParcial(CaraEncuestada, strTotalVenta, strVolumen);
                            }
                        }
                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|strTotalVenta = " + strTotalVenta + "  |strVolumen =" + strVolumen);
                        // SWRegistro.Flush(); //DCF Borra porbar parciales de despacho  
                        break;
                        #endregion;

                    case EstadoCara.A2FinDespacho:

                        break;

                    default:

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Estado Indeterminado: " + PropiedadesCara[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|TomarAccion: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        
        public bool ProcesoTomaLectura()
        {
            try
            {


                if (TX_Pool)
                {
                    //**************************** ****************************** *******************************************             
                    //****************************   17/06/2011 sugerencia del fabricante ****************************************
                    // Por Recomendacion del fabricante enviar 4 veces un Poll para que se registren los totalizadores en la EEprom 

                    for (int i = 0; i < 3; i++)
                    {
                        ProcesoEnvioComando(ComandoSurtidor.Pool);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Tiempo para calcular el totalizador -> EEPROM");
                        SWRegistro.Flush();
                    }
                    //**************************** ****************************** *******************************************
                    //**************************** ****************************** *******************************************
                    //**************************** ****************************** *******************************************
                    
                    //TX_Pool = false;
                }

                //ProcesoTomaLecturaImporte(); // Obtiene el totalizador de importe "ventas realizadas dinero "

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador)) //Volumen
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma Totalizador de Volumen  Exitoso: ");
                    SWRegistro.Flush();

                    //ProcesoEnvioComando(ComandoSurtidor.Ack);
                    return true;

                }

                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando de obtener Totalizador Volumen");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|ProcesoTomaLectura: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }
        
        public bool ProcesoTomaLecturaImporte()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizadorImporte))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma Totalizador de Importe Exitoso: ");
                    SWRegistro.Flush();
                    //ProcesoEnvioComando(ComandoSurtidor.Ack);
                    return true;


                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando de obtener ObtenerTotalizadorImporte_I");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|ObtenerTotalizadorImporte: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }
        
        //REALIZA PROCESO DE FIN DE VENTAF
        public void ProcesoFindeVenta()
        {
            try
            {

                //Inicializacion de variables
                PropiedadesCara[CaraEncuestada].Volumen = 0;
                PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                PropiedadesCara[CaraEncuestada].PrecioVenta = 0;

                //DCF borra los datos de la venta anteriro 15/12/2011

                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte = 0;
                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1 = 0;
                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta = 0;
                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final = 0;           
                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final = 0;

                ///******////******///***////////////////



                PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta;

                decimal VolumenCalculado = new decimal();
                decimal ImporteCalculadoLecturas_Diferencias = new decimal();
                decimal ImporteCalculado_PV_Vol = new decimal();


                //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 07-04-2011
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoVentaInicial +
                        " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta);
                    SWRegistro.Flush();
                }




                //
              
                
                
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDatosVenta)) //r ***** Retornar INformacion de Venta ******** regresa el precio, volumen e importe de la venta  y estado de venta finalizada
                {
                    //PropiedadesCara[CaraEncuestada].PrecioVenta =
                    //    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1;

                    //DCF comprobar que el surtido entregas datos de la venta finalizada. |02|08|xx|xx|xx|xx|zz|zz|zz|zz| xx = vol ;zz = Importe 
                    if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta == 0
                        && PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte == 0)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inconsistencia|NO se reporto datos de Venta: ");
                        SWRegistro.Flush();

                        int cont1 = 0;
                        while (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta == 0 && cont1 < 3)
                        {
                            ProcesoEnvioComando(ComandoSurtidor.ObtenerDatosVenta);

                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Repetición Obtener Datos Venta|Precio de Venta: " +
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1 + " |LecturaVenta de Venta: " +
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta + "|Importe: " +
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte);
                            SWRegistro.Flush();

                            cont1 += 1;

                        }


                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precio de Venta: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1 + " |LecturaVenta de Venta: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaVenta + "|Importe: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte);
                        SWRegistro.Flush();
                    }

                }

                //Modificado para disminuir las consulta y bajar los tiempo 25/01/2012. se hace en envio del POLL antes de Obtener el totalizador de volumen 

                //////**************************** ****************************** *******************************************             
                //////****************************   17/06/2011 sugerencia del fabricante ****************************************
                ////// Por Recomendacion del fabricante enviar 4 veces un Poll para que se registren los totalizadores en la EEprom 

                ////for (int i = 0; i < 3; i++)
                ////{
                ////    ProcesoEnvioComando(ComandoSurtidor.Poll);
                ////    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Tiempo para calcular el totalizador -> EEPROM");
                ////    SWRegistro.Flush();
                ////}
                //////**************************** ****************************** *******************************************
                //////**************************** ****************************** *******************************************
                //////**************************** ****************************** *******************************************

                TX_Pool = true;//Activar el envio del pool con un retardo de 50 ms entre el TX y RX 

                if (!ProcesoTomaLectura()) //Obtener Totalizador final de Volumen par diferencias de lecturas 
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                    SWRegistro.Flush();

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial; //DCF 29/06/2011
                }

                else
                {
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen;

                    VolumenCalculado = (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final -
                                 PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial);

                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma de Totalizador Final de Volumen: " +
                    //    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final);
                    //    SWRegistro.Flush(); // Borra solo para Inspección DCF *******                     

                }


                if (!ProcesoTomaLecturaImporte()) //Obtener Importe por diferencias de lecturas en totalizadores Importe
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Importe");
                    SWRegistro.Flush();

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial; //DCF             
                }

                else
                {
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma de Totalizador Importe Final Exitoso: " +
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final);
                    SWRegistro.Flush();


                    ImporteCalculadoLecturas_Diferencias = (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final -
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial);


                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte = 0;
                }


                // *************************** ************************* *********************
                // *************************** ************************* *********************
                // se toma de nuevo la toma de totalizador vol por ser igual a vol Inicial 29/06/2011

              
                int cont = 0;
                while ((PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final ==
                       PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial) && cont < 1)  //Modificado para disminuir las consulta y bajar los tiempo 25/01/2012. se hace en envio del POLL antes de Obtener el totalizador de volumen 
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Toma de Totalizador Volfinal = VolInicial = " +
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final);
                    SWRegistro.Flush();

                    ProcesoTomaLectura();
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen;

                    cont += 1;
                }
                // *************************** ************************* *********************
                // *************************** ************************* *********************



                //validacion de la ventas y los valores calculados y entregados por el surtidor:
                //Si el volumen LF-LI = Volumen entregado por el surtidor 
                if (VolumenCalculado == PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta)
                {// sei se cumple esta condicion la venta es correct
                    
                        //Importe correcto ImporteCalculado_PV_Vol = Vol * PV
                        ImporteCalculado_PV_Vol= (VolumenCalculado * PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1);

                        //Para corregir el factor Totalizador Importe  //ImporteCalculadoLecturas_Diferencias = LF-LI -- 
                        if (ImporteCalculadoLecturas_Diferencias != PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte)//PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].LecturaImporte)
                        {
                           PropiedadesCara[CaraEncuestada].conta_factorImporte += 1;

                            //se realiza un conteo para determinar que si es necesario cambiar el valor del factor. 
                           if (PropiedadesCara[CaraEncuestada].conta_factorImporte > 2)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso| FactorTotalizadorImporte =" + PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte + "ImporteCalculadoLecturas_Diferencias = " + ImporteCalculadoLecturas_Diferencias );
                                SWRegistro.Flush();

                                if ((ImporteCalculadoLecturas_Diferencias * 10) == PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte)
                                    PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte = PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte / 10;

                                if ((ImporteCalculadoLecturas_Diferencias * 100) == PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte)
                                    PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte = PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte / 100;

                                if ((ImporteCalculadoLecturas_Diferencias * 1000) == PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte)
                                    PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte = PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte / 1000;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso| FactorTotalizadorImporte Cambiado =" + PropiedadesCara[CaraEncuestada].FactorTotalizadorImporte);
                                SWRegistro.Flush();
                            }
                        }
                        else
                            PropiedadesCara[CaraEncuestada].conta_factorImporte = 0;


                        decimal Porcentaje =  ((ImporteCalculado_PV_Vol * 10)/100); //10% para filtrar el corrimiento de comas en los surtidores y no filtara las venta sin el redondeo por parte del surtidor 
                
                        //Importe calculado Vol X PV, comparado con el importe reportado por el surtidor // compara con un valor de porcentaje 
                        if (( PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte > (ImporteCalculado_PV_Vol + Porcentaje)
                            ||PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte < (ImporteCalculado_PV_Vol - Porcentaje)))
                       
                        {
                            
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Falla|ImporteCalculado difiere +-10% de Importe Reportado" + ". Importe Reportado =" +
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte +
                           " - ImporteCalculado PV * Vol =" + ImporteCalculado_PV_Vol);
                            SWRegistro.Flush();

                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte = ImporteCalculado_PV_Vol;

                        }

                }
                

             

                if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta != 0)
                ////Evalúa si la venta viene en 0
                //if (PropiedadesCara[CaraEncuestada].Volumen != 0 || PropiedadesCara[CaraEncuestada].TotalVenta != 0)
                {
                    //Almacena los valores en las variables requerida por el Evento

                    string strTotalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaImporte.ToString("N2"); //PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");//Importe
                    string strPrecio = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1.ToString("N2");//PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final.ToString("N2");//PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial.ToString("N2");
                    string strVolumen = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaVenta.ToString("N2");//PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                    string strLecturaFinalImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final.ToString("N2"); //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte.ToString("N3"); //DCF enviar a DB
                    string strLecturaInicialImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial.ToString("N2");//DCF enviar a DB

                    byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                 

                    //Loguea evento Fin de Venta
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                        " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                        " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                    SWRegistro.Flush();


                    String PresionLLenado = "0";
                    string[] Args = { CaraEncuestada.ToString(), strTotalVenta.ToString(), strPrecio.ToString(), strLecturaFinalVenta.ToString(), strVolumen.ToString(), bytProducto.ToString(), IdManguera.ToString(), PresionLLenado.ToString(), strLecturaInicialVenta.ToString() };

                    //                      string Args = CaraEncuestada.ToString() + "|" + strTotalVenta.ToString() + "|" + strPrecio.ToString() + "|" + strLecturaFinalVenta.ToString() + "|" + strVolumen.ToString() + "|" + bytProducto.ToString() + "|" + IdManguera.ToString() + "|" + PresionLLenado.ToString() + "|" + strLecturaInicialVenta.ToString();

                    Thread HiloFinalizacionVenta = new Thread(InformarFinalizacionVenta);
                    HiloFinalizacionVenta.Start(Args);


                    //oEvento.InformarFinalizacionVenta( CaraEncuestada,  strTotalVenta,  strPrecio,  strLecturaFinalVenta,
                    //           strVolumen,  bytProducto,  IdManguera,  PresionLLenado,  strLecturaInicialVenta);


                    ////if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 12-03-11 
                    ////{//Se asegura obtener siempre la lectura Inicial de volumen e Importe, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final;

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final;

                    ////}

                    //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false; //05-06-2013 dcf

                    Thread.Sleep(300);//para dar espera y no afectar al proceso de inserción de venta //30-07-2013  Juan David -- Agrego 300 Milisegundos de Retardo 

                    if (CaraEnReposo != null)
                    {
                        CaraEnReposo(CaraEncuestada, IdManguera);
                    }
                    //05-06-2013 dcf
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera1. Grado " + IdManguera);
                    SWRegistro.Flush();
                }


                /// Datos de Ventas Calculadas.  
               //else if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final !=
               //          PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial)

                //else if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final !=
                //        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial &&
                //        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial > 0) //si entrega Li = 0 es que se reinicio el sitema 

               
                else if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final !=
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial &&
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial > 0 && ImporteCalculadoLecturas_Diferencias > 0) //se filtra el ImporteCalculado si es = 0 no se realizo venta  salta a ventas en CERO 09/02/2011 - 11:38
           
                {
                        
                    //Almacena los valores en las variables requerida por el Evento
                    string strVolumen = VolumenCalculado.ToString("N2");
                    string strTotalVenta = ImporteCalculadoLecturas_Diferencias.ToString("N2"); 
                    string strPrecio = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1.ToString("N2");//PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final.ToString("N2");//PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial.ToString("N2");
                    string strLecturaFinalImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final.ToString("N2"); //PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalImporte.ToString("N3"); //DCF enviar a DB
                    string strLecturaInicialImporte = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial.ToString("N2");//DCF enviar a DB

                    byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                    //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                    //Loguea evento Fin de Venta
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|InformarFinalizacionVenta Ventas Calculadas. Importe: " + strTotalVenta +
                        " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                        " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                    SWRegistro.Flush();

                    String PresionLLenado = "0";
                   

                    if (VentaFinalizada != null)
                    {
                        VentaFinalizada(CaraEncuestada, strTotalVenta, strPrecio, strLecturaFinalVenta,
                                  strVolumen, bytProducto.ToString(), IdManguera, PresionLLenado, strLecturaInicialVenta);

                    }



                    ////if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 12-03-11 
                    ////{//Se asegura obtener siempre la lectura Inicial de volumen e Importe, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Inicial =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorVolumen_Final;

                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Inicial =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].TotalizadorImporte_Final;

                    ////}

                    //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false; //05-06-2013 dcf
                    
                    Thread.Sleep(100);//para dar espera y no afectar al proceso de inserción de venta 

                    if (CaraEnReposo != null)
                    {
                        CaraEnReposo(CaraEncuestada, IdManguera);
                    }
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa cara en Espera2. Grado " + IdManguera);
                    SWRegistro.Flush();
                }




                else  //las lecturas son = 0 Venta en Cero 
                {

                    //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada       
                    if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 07-04-2011
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoVentaInicial +
                            " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta + " |Proceso|ProcesoFindeVenta ");
                        SWRegistro.Flush();

                        PropiedadesCara[CaraEncuestada].GradoVenta = PropiedadesCara[CaraEncuestada].GradoVentaInicial;


                        ProcesoFindeVenta(); // Se toman de nuevo las lecturas por error en el Grado que reporto la venta.
                    }
                    else
                    {

                        if (VentaInterrumpidaEnCero != null)
                        {
                            VentaInterrumpidaEnCero(CaraEncuestada);
                        }

                        PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Venta en CERO");
                        SWRegistro.Flush();

                    }
                }



                //else
                //{
                //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No acepto comando de obtencion de Precio en Final de Venta");
                //    SWRegistro.Flush();
                //}


                PropiedadesCara[CaraEncuestada].EsVentaParcial = false; //Borrara solo pruebas DCF

                TX_Pool = false;//Desactiva el envio del pool con un retardo de 50 ms entre el TX y RX 


            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|ProcesoFindeVenta: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        
        public void InformarFinalizacionVenta(object args)
        {
            string[] Argumentos = (string[])args;
           
            byte CaraEncuestadaFinVenta = Convert.ToByte(Argumentos[0]);
            string strTotalVenta = Argumentos[1];
            string strPrecio = Argumentos[2];
            string strLecturaFinalVenta = Argumentos[3];
            string strVolumen = Argumentos[4];
            string bytProducto = Convert.ToString(Argumentos[5]);
            int IdManguera = Convert.ToInt32(Argumentos[6]);
            string PresionLLenado = Argumentos[7];
            string strLecturaInicialVenta = Argumentos[8];



            if (VentaFinalizada != null)
            {
                VentaFinalizada(CaraEncuestada, strTotalVenta, strPrecio, strLecturaFinalVenta,
                          strVolumen, bytProducto, IdManguera, PresionLLenado, strLecturaInicialVenta);

            }


        }
        
        public void LecturaAperturaCierre()
        {
            try
            {
                bool TomaLecturasExitoso = true;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                foreach (Grados Grado in PropiedadesCara[CaraEncuestada].ListaGrados) //Recorre las mangueras de la cara encuestada
                {
                    PropiedadesCara[CaraEncuestada].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                    if (ProcesoTomaLectura())
                    {
                        //Arma Arreglo de lecturas
                        ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].TotalizadorVolumen) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Arma Lecturas para turno. Manguera " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].TotalizadorVolumen);
                        SWRegistro.Flush();

                        //Cambia el precio si es apertura de turno
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia cambio de precios");
                            SWRegistro.Flush();

                            ProcesoCambioPrecio();
                        }
                    }
                    else
                    {
                        TomaLecturasExitoso = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando de obtener Totalizador para Lectura Inicial/Final de Turno. Grado: " + Grado.NoGrado);
                        SWRegistro.Flush();
                        break;
                    }
                }

                if (TomaLecturasExitoso)
                {
                    System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                    ArrayLecturas.CopyTo(LecturasEnvio);

                    //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                    {
                        if (LecturaTurnoCerrado != null)
                        {
                            LecturaTurnoCerrado(LecturasEnvio);
                        }

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lecturas Finales de turno");
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                    }
                    //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                    if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                    {
                        if (LecturaTurnoAbierto != null)
                        {
                            LecturaTurnoAbierto(LecturasEnvio);
                        }
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lecturas Iniciales de turno");
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|LecturaAperturaCierre: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void ProcesoCambioPrecio()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Cambio Precio Grado " +
                    PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                SWRegistro.Flush();

                if (ProcesoEnvioComando(ComandoSurtidor.EstalecerPrecio))
                {
                    if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 ==
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precio aceptado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precio rechazado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Fallo|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|ProcesoCambioPrecio: " + Excepcion);
                SWRegistro.Flush();
            }
        }


    



        #endregion

        #region EVENTOS DE LA CLASE

        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    PropiedadesCara[Cara].AutorizarCara = true; //se activa sin abrir turno ???

                    //Valor a programar
                    PropiedadesCara[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    PropiedadesCara[Cara].PrecioVenta = Convert.ToDecimal(Precio);

                    PropiedadesCara[Cara].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (PropiedadesCara[Cara].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PropiedadesCara[Cara].PredeterminarImporte = false;
                            PropiedadesCara[Cara].PredeterminarVolumen = true;
                        }
                        else
                        {
                            PropiedadesCara[Cara].PredeterminarImporte = true;
                            PropiedadesCara[Cara].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        PropiedadesCara[Cara].PredeterminarImporte = false;
                        PropiedadesCara[Cara].PredeterminarVolumen = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEvento_VentaAutorizada: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_FinalizarCambioTarjeta(byte Cara)
        {
        }    

        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto)
        {
        
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
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraLectura].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

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
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraLectura].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;

                            }

                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoAbierto: " + Excepcion);
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
                        if (PropiedadesCara.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraLectura].TomarLecturaCierreTurno = true;

                            }
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
                                PropiedadesCara[CaraLectura].TomarLecturaCierreTurno = true;
                            }
                        }
                    }
                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Surtidores + "|Excepcion|oEvento_TurnoCerrado: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //Evento que manda a cambiar el producto y su respectivo precio en las mangueras

        
        public void Evento_ProgramarCambioPrecioKardex( ColMangueras mangueras)
        {
            try
            {
                //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
                foreach (Manguera OManguera in mangueras)
                {
                    foreach (RedSurtidor ORedSurtidor in PropiedadesCara.Values)
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
                                    " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
                                SWRegistro.Flush();
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_ProgramarCambioPrecioKardex: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        
        
        
        public void Evento_FinalizarVentaPorMonitoreoCHIP( byte Cara)
        {
            try
            {
                if (PropiedadesCara.ContainsKey(Cara))
                {
                    PropiedadesCara[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(Cara) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEVento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion);
                SWRegistro.Flush();
            }

        }
        public void Evento_CerrarProtocolo()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                CondicionCiclo = false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|oEventoCerrarProtocolo: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        public void Evento_CancelarVenta(byte Cara)
        {
    
        } 

        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            //Metodo de la interfaz Iprotocolo, solo se usa en el protocolo MR3
        }


        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor) //Utilizado para solicitud de lecturas por surtidor - Manguera
        {
        }


        #endregion
    }
}
