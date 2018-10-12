
using System;
using System.Collections.Generic;
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Text;
using System.Threading;         //Para manejo del Timer
using System.Timers;            //Para manejo del Timer
using System.Windows.Forms;
using POSstation.Protocolos;     //Para alcanzar la ruta de los ejecutables

namespace POSstation.Protocolos
{
    public class EMR3 : iProtocolo
    {
        #region EventoDeProtocolo

        private bool TipoAplicacion = false;
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

        public enum KeysDevice
        {   Start = 0, Finish, Mode, Preset, Next, Plus, Minus, Cler, Enter, keypad,
            K1, K2, K3, K4, K5, K6, K7, K8, K9
        }
      

        #endregion
        #region DECLARACION DE VARIABLES Y DEFINICIONES

        Dictionary<byte, RedSurtidor> PropiedadesCara;        //Diccionario donde se almacenan las Caras y sus propiedades

        public enum ComandoSurtidor
        {

            //Mensajes de Pedido de Informacion
            Star,
            Pausa,
            Detener,
            Autorizar,
            Desautorizar,
            Estado,
            Estado_Delivery,
            Estado_impresora,
            ObtenerVentaVolumen,
            ObtenerVentaVolumen_String,
            ObtenerTotalizador,
            ObtenerTotalizador_String,
            EstalecerPrecio,
            ObtenerPrecio,
            Predeterminar,
            ObtenerVentaDinero,




        }   //Define los COMANDOS que se envian al Surtidor

        ComandoSurtidor ComandoCaras = ComandoSurtidor.Star;

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        byte GradoEncuesta;             // Grado o Manguera a Encuestar
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no
        string AuxiliarLectura;

        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES


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
        byte CaraID;//DCF Alias
        byte CaraTmp; // Utilizado para las caras con alias mas de 16 caras

        decimal difer; //diferencia en Lf- Li


        int CRC = 0;
        byte[] Precio_ = new byte[4];
        decimal Dat = 0;
        int BytesEsperados = 0;
        int BytesRecibidos = 0;
        int BytesEsperados2 = 0;
        int ElementoInicial = 0;
        int Longitud = 0;
        decimal Value;
        string PuertoRS232;
        string Puerto;

        #endregion

        #region PUNTO DE ARRANQUE
        //PUNTO DE ARRANQUE DE LA CLASE
        public EMR3(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                this.Puerto = Puerto;

                if (!Directory.Exists(Application.StartupPath + "/LogueoProtocolo"))
                {
                    Directory.CreateDirectory(Application.StartupPath + "/LogueoProtocolo/");
                }
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-EMR3-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-EMR3-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                // SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 23-04-2013 -1544"); //Environment.CurrentDirectory  por  Application.StartupPath
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 26-06-2013 -1153"); //Environment.CurrentDirectory  por  Application.StartupPath
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 01-07-2013 -1530"); //Environment.CurrentDirectory  por  Application.StartupPath
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 04-07-2013 -0756"); //Totalizador en Sting- no se comprueba cantidad de byte en RX 
                //SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 09-07-2014 - 0855"); //Totalizador en Sting- no se comprueba cantidad de byte en RX 
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. EMR3 09-07-2014 - 0855");//DCF Archivos .txt 08/03/2018  

                SWRegistro.Flush();

                //Instancia los eventos disparados por la aplicacion cliente

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoRS232 = Puerto;
                    PuertoCom.PortName = PuertoRS232;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.DataBits = 8;
                    PuertoCom.Open();
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();

                }

                //PropiedadesCara es la referencia con la que se va a trabajar
                PropiedadesCara = new Dictionary<byte, RedSurtidor>();
                PropiedadesCara = EstructuraCaras;

                foreach (RedSurtidor oCara in PropiedadesCara.Values)
                {
                    foreach (Grados oGrado in PropiedadesCara[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1 + " - Venta Parcial: " + oCara.EsVentaParcial);
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


        private byte ConvertirCaraBD(byte caraBD) //YEZID Alias de las caras //DCF 2011-05-14
        {
            byte CaraSurtidor = 0;
            try
            {
                foreach (RedSurtidor ORedCaras in PropiedadesCara.Values)
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
        private void CicloCara()
        {
            try
            {
                //Variable para garantizar el ciclo infinito
                CondicionCiclo = true;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Inicia ciclo de encuesta a " + PropiedadesCara.Count + " caras");
                SWRegistro.Flush();

                this.Tecla_Fin();
                Thread.Sleep(1000);
                Evento_CancelarVenta(0x01); // reinicia siempre al iniciar la cara 1
                Thread.Sleep(500);
                this.Tecla_Fin();

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
                            CaraEncuestada = ORedCaras.Cara;

                            CaraID = PropiedadesCara[CaraEncuestada].CaraBD; //Cara consecutiva DCF Alias

                            if (ProcesoEnvioComando(ComandoSurtidor.Estado)) //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                                TomarAccion();


                        }
                        Thread.Sleep(500);
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Wayne: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        #endregion

        public void VerifySizeFile()
        {
            try
            {
                FileInfo FileInf = new FileInfo(ArchivoTramas);//DCF Archivos .txt 08/03/2018  

                if (FileInf.Length > 50000000)
                {
                    SWTramas.Close();
                    ArchivoTramas = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-EMR3-Tramas.(" + Puerto + ").txt";
                    SWTramas = File.AppendText(ArchivoTramas);
                }


                //FileInfo 
                FileInf = new FileInfo(Archivo);
                if (FileInf.Length > 30000000)
                {
                    SWRegistro.Close();
                    //Crea archivo para almacenar inconsistencias en el proceso logico
                    Archivo = Application.StartupPath + "/LogueoProtocolo/" + DateTime.Now.ToString("yyyyMMdd") + "-EMR3-Sucesos(" + Puerto + ").txt";
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
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 4;// antes 2 DCF

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                int Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;

                //Arma la trama de Transmision
                ArmarTramaTx();

                do
                {

                    if (FalloComunicacion)//Si el equipo no responde espera 1 seg u pregunta de nuevo y espera 500 ms para leer:
                    {
                        Thread.Sleep(1000);
                        TimeOut = 500;
                    }

                    EnviarComando();
                    //Analiza la información recibida si se espera respuesta del Surtidor
                    RecibirInformacion();
                    Reintentos += 1;
                } while ((FalloComunicacion == true && Reintentos < MaximoReintento));


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
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                            if (CancelarProcesarTurno != null)
                            {
                                CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (!PropiedadesCara[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Perdida de comunicacion en " + ComandoaEnviar);
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
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Se reestablece comunciación con surtidor en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoEnviocomando: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        private string ModificarFormatoDecimal(string valor)
        {
            try
            {
                Decimal resultado = 0;

                if (valor.IndexOf(".") > 0)
                    resultado = Convert.ToDecimal(valor.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator));
                else
                    resultado = Convert.ToDecimal(valor);

                return resultado.ToString();
            }

            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ModificarFormatoDecimal: " + Excepcion);
                SWRegistro.Flush();

                return "";
            }

        }

        private void Dato_Hex(string dato, ref int Byte_)
        {
            try
            {
                switch (dato)
                {
                    case "0000":
                        Byte_ = 0x00;
                        break;

                    case "0001":
                        Byte_ = 0x01;
                        break;

                    case "0010":
                        Byte_ = 0x02;
                        break;

                    case "0011":
                        Byte_ = 0x03;
                        break;

                    case "0100":
                        Byte_ = 0x04;
                        break;

                    case "0101":
                        Byte_ = 0x05;
                        break;

                    case "0110":
                        Byte_ = 0x06;
                        break;

                    case "0111":
                        Byte_ = 0x07;
                        break;

                    case "1000":
                        Byte_ = 0x08;
                        break;

                    case "1001":
                        Byte_ = 0x09;
                        break;

                    case "1010":
                        Byte_ = 0x0A;
                        break;

                    case "1011":
                        Byte_ = 0x0B;
                        break;

                    case "1100":
                        Byte_ = 0x0C;
                        break;

                    case "1101":
                        Byte_ = 0x0D;
                        break;

                    case "1110":
                        Byte_ = 0x0E;
                        break;

                    case "1111":
                        Byte_ = 0x0F;
                        break;

                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Dato_Hex: " + Excepcion);
                SWRegistro.Flush();
            }


        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx()
        {
            try
            {

                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Star:
                        TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'O', 0X01, 0X01, (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.Pausa:
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'O', 0X02, (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.Detener:
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'O', 0X03, (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.Autorizar:
                        TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'O', 0X06, 0x01, (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 250;
                        break;

                    case ComandoSurtidor.Desautorizar:
                        TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'O', 0X06, 0x00, (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 350;
                        break;

                    case ComandoSurtidor.Estado:
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'T', 0x01, (byte)(CRC), 0X7E };
                        BytesEsperados = 8;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.Estado_impresora:
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'T', 0x02, (byte)(CRC), 0X7E };
                        BytesEsperados = 8;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.Estado_Delivery:
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'T', 0x03, (byte)(CRC), 0X7E };
                        BytesEsperados = 9;
                        TimeOut = 200;
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen://K Mayúscula
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'G', (byte)'K', (byte)(CRC), 0X7E };
                        BytesEsperados = 15;
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen_String: //k Minuscula
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'G', (byte)'k', (byte)(CRC), 0X7E };
                        BytesEsperados = 16;
                        BytesEsperados2 = 17;
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.ObtenerTotalizador://L Mayúscula
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'G', (byte)'L', (byte)(CRC), 0X7E };
                        BytesEsperados = 15;
                      
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.ObtenerTotalizador_String://l Minuscula
                        TramaTx = new byte[7] { 0x7E, 0X01, 0XFF, (byte)'G', (byte)'l', (byte)(CRC), 0X7E };
                        BytesEsperados = 15;
                        BytesEsperados2 = 17;
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.EstalecerPrecio:

                        envio_digitos(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                        TramaTx = new byte[13] { 0x7E, 0X01, 0XFF, 0x44, 0x00, CaraEncuestada, CaraEncuestada, Precio_[0], Precio_[1], Precio_[2], Precio_[3], (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.ObtenerPrecio:
                        TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'E', 0x00, CaraEncuestada, (byte)(CRC), 0X7E };
                        BytesEsperados = 13;
                        TimeOut = 500;
                        break;

                    case ComandoSurtidor.Predeterminar: //Volumen 
                        envio_digitos(PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                        TramaTx = new byte[11] { 0x7E, 0X01, 0XFF, (byte)'S', (byte)'c', Precio_[0], Precio_[1], Precio_[2], Precio_[3], (byte)(CRC), 0X7E };
                        BytesEsperados = 7;
                        TimeOut = 1000;
                        break;


                }

                //Calcula el CRC 
                CalcularCRC(TramaTx);

                TramaTx[TramaTx.Length - 2] = (byte)CRC; //Agreag el Byte del CRC a la Trama TX  

                List<byte> lst = new List<byte>();
                lst.AddRange(TramaTx);
                byte t, scape;
                int p;

                t = 0x7E;
                p = lst.IndexOf(t);
                if (!(p > 0 && p < lst.Count)) p = -1; else p++;

                if (p < 0)
                {
                    t = 0x7D;
                    p = lst.IndexOf(0x7D);
                    if (!(p > 0 && p < lst.Count)) p = -1; else p++;
                }

                if (p >= 0)
                {
                    scape = ((byte)(0x20 ^ t));
                    lst.Insert(p, scape);

                    TramaTx = lst.ToArray();

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Se modifica caracteres prohibidos TramaTx. ");
                    SWRegistro.Flush();
                }


                
            }

            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void envio_digitos(decimal Valor)//4 Byte Float = 32 Bit
        {
            try
            {
                Dat = Valor;

                char[] Vector_IEEE_32 = new char[32];
                TramaTx = new byte[13];//Trama para enviar el precio 


                for (int i = 0; i < 32; i++) //Cargo todo los bytes en 0 = 30 hex
                    Vector_IEEE_32[i] = '0';

                //Precio 
                //string precio_TX = (SetPrecio.Text).ToString();

                //asignacion de signo 1= - // 0 = +
                if (Dat < 0)
                {
                    Vector_IEEE_32[0] = '1'; //-
                }
                else
                    Vector_IEEE_32[0] = '0';//+


                //Modifica el separador decimal por el del equipo.
                //Dat = Convert.ToDecimal(ModificarFormatoDecimal(SetPrecio.Text));

                //Obtener parte Entera
                decimal Entero = Math.Truncate(Dat);

                //obtener parte Decimal y convertirla a Binario tipo string 
                decimal Dec = Dat - Entero;
                decimal frac = Dec;
                int conta = 0;
                string bina_dec = "";
                while (frac != 1 && conta < 23)
                {
                    frac = frac * 2;

                    if (frac > 1)
                    {
                        bina_dec += "1";
                        frac = frac - 1;
                    }
                    else
                        bina_dec += "0";

                    conta++;
                }

                Int64 Dato_preci = Convert.ToInt64(Entero);
                Int64 potencia = 0;
                double BIt = 0;
                string binario = "";
                string Mantisa = "";
                bool Activa_punto = false;
                Int16 nume_expo = 0;

                for (int j = 31; j >= 0; j--)
                {
                    potencia = Convert.ToInt64(Math.Pow(2, j)); //2^7, 2^6, 2^5, 2^4, 2^3, 2^2, 2^1, 2^0, 
                    //128, 64,  32,   16,  8,  4,   2,   1
                    BIt = (Dato_preci & potencia) / potencia;

                    if (Activa_punto)
                    {
                        nume_expo++;
                        Mantisa += Convert.ToString(BIt);
                    }

                    binario += Convert.ToString(BIt);

                    if (BIt == 1)
                        Activa_punto = true;
                }

                int exponente_dec = 127 + nume_expo;
                string exponente_String = Convert.ToString(exponente_dec, 2).PadLeft(8, '0');//convierte el dato a Binario.

                int h = 1;
                for (h = 1; h < 9; h++)//Insercion del exponente al vector
                {
                    //Vector_IEEE_32[h] = Convert.ToByte(Convert.ToInt16(exponente_String[h-1]));
                    Vector_IEEE_32[h] = Convert.ToChar(exponente_String[h - 1]);
                }

                for (h = 9; h < Mantisa.Length + 9; h++)//Insercion Mantisa
                {
                    Vector_IEEE_32[h] = Convert.ToChar(Mantisa[h - 9]);
                }

                int f = 0;

                for (h = h; h < 32; h++)
                {
                    Vector_IEEE_32[h] = Convert.ToChar(bina_dec[f]);
                    f++;
                }

                //convertir los bytes  a hex******************************************************************************
                string byte_hex = "";
                int L = 0;
                int H = 0;
                int byteH = 0;
                int byteL = 0;

                for (int n = 0; n <= 32; n++)
                {
                    if (L == 4)
                    {
                        Dato_Hex(byte_hex, ref byteL);
                        byte_hex = "";
                        byteL = byteL << 4;
                    }

                    if (L == 8)
                    {
                        Dato_Hex(byte_hex, ref byteH);
                        byte_hex = "";
                        L = 0;
                        int cccn = byteL + byteH;
                        Precio_[3 - H] = (byte)cccn;
                        H++;
                        //byteH = byteH << 4;
                    }

                    if (n <= 31)
                        byte_hex += Vector_IEEE_32[n];

                    L++;
                }

            }
            catch (Exception Excepcion)
            {

            }

        }

        //Realiza el calculo del CRC
        private void CalcularCRC(byte[] Trama)
        {
            try
            {
                CRC = 0x00;

                for (int i = 1; i < Trama.Length - 2; i++)
                {
                    CRC += Trama[i];
                }

                CRC = (0 - CRC) & 255;

            }

            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ArmarTramaTx: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private Decimal ObtenerValor(int ElementoInicial, int Longitud)
        {
            string doublestr = "";
            string input = "";
            decimal rounded = 0;

            try
            {

                for (int i = (TramaRx.Length - 3); i >= (ElementoInicial); i--)
                {
                    //00 00 00 00 00 DC 83 40 BA 7E                 
                    input += TramaRx[i].ToString("X2");
                }

                UInt64 bigendian;
                //consulta si la cadena de string es validad, y convierte es string a hex = out bigendian
                bool success = UInt64.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out bigendian);
                if (success)
                {
                    double fractionDivide = Math.Pow(2, 23);
                    double doubleout;

                    int sign = (bigendian & 0x80000000) == 0 ? 1 : -1;
                    //0x7F800000 = 11111111 00000000000000000000000 //corre 23 posiciones a la derecha... ceros "0"
                    Int64 exponent = ((Int64)(bigendian & 0x7F800000) >> 23) - (Int64)127;
                    UInt64 fraction = (bigendian & 0x007FFFFF); //0000 0000 0111 1111 1111 1111 1111 1111
                    if (fraction == 0)
                        doubleout = sign * Math.Pow(2, exponent);
                    else
                        doubleout = sign * (1 + (fraction / fractionDivide)) * Math.Pow(2, exponent);

                    rounded = decimal.Round(Convert.ToDecimal(doubleout), 2);
                    doublestr = doubleout.ToString();

                    return rounded;
                }

                return rounded;

            }

            catch (Exception Excepcion)
            {

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|en el metodo ObtenerValor: " + Excepcion);
                SWRegistro.Flush();

                return rounded;
            }
        }

        private decimal ObtenerValor16(int ElementoInicial, int Longitud)
        {
            string input = "";
            Value = 0;
            try
            {
                //INvierto la posicion del Daton en TramaRX
                for (int i = (TramaRx.Length - 3); i >= (ElementoInicial); i--)
                {
                    //00 00 00 00 00 DC 83 40 BA 7E                 
                    input += TramaRx[i].ToString("X2");
                }

                UInt64 bigendian;
                //consulta si la cadena de string es validad, y convierte es string a hex = out bigendian
                bool success = UInt64.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out bigendian);
                if (success)
                {
                    double fractionDivide = Math.Pow(2, 52);

                    int sign = (bigendian & 0x8000000000000000) == 0 ? 1 : -1;
                    //0x7FF0000000000000 = 111 1111 1111 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 //corre 23 posiciones a la derecha... ceros "0"
                    Int64 E = ((Int64)(bigendian & 0x7FF0000000000000) >> 52) - (Int64)1023;
                    double Exponente = Math.Pow(2, E);
                    UInt64 M = (bigendian & 0x000FFFFFFFFFFFFF); //0000 0000 0000 1111 1111 1111 1111 1111 1111 1111 1111 1111 1111 1111 1111 1111
                    double Mantisa = (1 + (M / fractionDivide));
                    Value = Convert.ToDecimal(sign * Exponente * Mantisa);

                    return Value;
                    //IEEE 64bit Flot
                }

                return Value;
            }

            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en ObtenerValor16: " + Excepcion;
                MessageBox.Show(MensajeExcepcion);

                return Value;
            }
        }

        private decimal ObtenerValor_String(int ElementoInicial, int Longitud)
        {
            Decimal resultado = 0;

            byte[] Data = new byte[9];
            //INvierto la posicion del Daton en TramaRX
            int j = 0;
            for (int i = ElementoInicial; i <= Longitud; i++, j++)
            {
                Data[j] = TramaRx[i];
            }

            string str = ASCIIEncoding.ASCII.GetString(Data);

            if (str.IndexOf(".") > 0)
                resultado = Convert.ToDecimal(str.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator));
            else
                resultado = Convert.ToDecimal(str);

            return resultado;
        }

        private decimal Volumen_string(int ElementoInicial, int Longitud)
        {
            Decimal resultado = 0;

            byte[] Data = new byte[TramaRx.Length - 9];
            //INvierto la posicion del Daton en TramaRX
            int j = 0;
            for (int i = ElementoInicial; i <= Longitud; i++, j++)
            {
                if (TramaRx[i] != ',')
                    Data[j] = TramaRx[i];
                else
                    j = j - 1;
            }

            string str = ASCIIEncoding.ASCII.GetString(Data);

            if (str.IndexOf(".") > 0)
                resultado = Convert.ToDecimal(str.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator));
            else
                resultado = Convert.ToDecimal(str);

            return resultado;
        }


        //ENVIA EL COMANDO AL SURTIDOR
        private void EnviarComando()
        {
            try
            {

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {
                    PuertoCom.PortName = PuertoRS232;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.None;
                    PuertoCom.DataBits = 8;
                    PuertoCom.Open();
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();

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
                    "|" + CaraID + "|Tx|" + ComandoCaras + "|" + strTrama);
                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                Thread.Sleep(TimeOut);

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|EnviarComando: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                BytesRecibidos = Bytes;

                if (Bytes > 0)
                {

                    if (!TramaEco)
                        eco = 0;

                    //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados

                    Thread.Sleep(200);
                    this.FalloComunicacion = true;

                    //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                    //if (Bytes == BytesEsperados || Bytes == BytesEsperados2)
                    //{
                    //Definicion de Trama Temporal
                    byte[] TramaTemporal = new byte[Bytes];

                    //Almacena informacion en la Trama Temporal para luego eliminarle el eco
                    PuertoCom.Read(TramaTemporal, 0, Bytes);
                    PuertoCom.DiscardInBuffer();

                    //Se dimensiona la Trama a evaluarse (TramaRx)
                    TramaRx = new byte[TramaTemporal.Length - eco];

                    //Almacena los datos reales (sin eco) en TramaRx
                    for (int i = 0; i <= (TramaTemporal.Length - eco - 1); i++)
                        TramaRx[i] = TramaTemporal[i + eco];

                    /////////////////////////////////////////////////////////////////////////////////
                    //LOGUEO DE TRAMA RECIBIDA
                    string strTrama = "";
                    for (int i = 0; i <= TramaRx.Length - 1; i++)
                        strTrama += TramaRx[i].ToString("X2") + "|";

                    SWTramas.WriteLine(
                        DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                        DateTime.Now.Year.ToString().PadLeft(4, '0') + "|" +
                        DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                        DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                         "|" + CaraID + "|Rx|" + ComandoCaras + "|" + strTrama);

                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////

                    //Revisa si existe problemas en la trama
                    if (ComprobarIntegridadTrama())
                    {
                        FalloComunicacion = false;
                        AnalizarTrama();
                    }

                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Comando " + ComandoCaras + ". Bytes con fallo en integridad de trama");
                        SWRegistro.Flush();
                    }
                    // }


                    //Thread.Sleep(10);

                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|RecibirInformacion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {
                if (TramaRx.Length > 0)
                {

                    if (TramaRx[0] == 0x7E && TramaRx[TramaRx.Length - 1] == 0x7E) //Inicio de Trama 0x7E y fin de Trama 0X7E
                    {
                        for (int i = 1; i < TramaRx.Length - 1; i++)
                        {
                            if (TramaRx[i] == 0x7D)
                            {
                                int Escape = 0x20 ^ TramaRx[i + 1];

                                byte[] TramaRX_ = new byte[TramaRx.Length - 1];

                                Array.Copy(TramaRx, TramaRX_, TramaRx.Length - 1);

                                //TramaRX_[i] = 0x7D;//Escape antes del New B.
                                TramaRX_[i] = (byte)Escape;//Nuevo Byte                         


                                for (int j = 1; j < TramaRX_.Length - i; j++)
                                {
                                    TramaRX_[j + i] = TramaRx[j + i + 1];
                                }

                                TramaRx = new byte[TramaRx.Length - 1];


                                Array.Copy(TramaRX_, TramaRx, TramaRx.Length);

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Se modifica caracteres prohibidos en TramaRx. ");
                                SWRegistro.Flush();

                            }

                        }


                        //Calcula el CRC 
                        CalcularCRC(TramaRx);

                        if (CRC == TramaRx[TramaRx.Length - 2])
                            return true;
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|CRC Errado error en los Byte Recibidos: " + TramaRx[TramaRx.Length - 1]);
                            SWRegistro.Flush();
                            return false;
                        }
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fin de Trama Erroneo: " + TramaRx[12].ToString("X2"));
                        SWRegistro.Flush();
                        return false;
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|NO se Obtuvo Respuesta en TramaRx  ");
                    SWRegistro.Flush();
                    return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ComprobacionIntegridadTrama: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        #endregion

        static int intentos = 0;

        public void SolicitarLecturasSurtidor(ref string Lecturas, string Surtidor)
        {

        }

        #region ANALISIS DE TRAMAS Y RECONSTRUCCIÓN DE DATOS PROVENIENTE DEL SURTIDOR
        //ANALIZA LA TRAMA, DEPENDIENDO DEL COMANDO ENVIADO
        private void AnalizarTrama()
        {

            try
            {
                switch (ComandoCaras)
                {
                    case ComandoSurtidor.Estado:
                        RecuperarEstado();
                        break;


                    case ComandoSurtidor.ObtenerPrecio:
                        ElementoInicial = 7;
                        Longitud = (TramaRx.Length - (ElementoInicial + 2));
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1
                            = ObtenerValor(ElementoInicial, Longitud);

                        // SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|ObtenerPrecio: " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1);
                        //SWRegistro.Flush(); //DCF Borra

                        break;

                    case ComandoSurtidor.EstalecerPrecio:
                        if (TramaRx[4] == 0)
                        {
                            //string Mensaje = "Precio Aceptado";

                            FalloComunicacion = false;
                            //SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|" + Mensaje);
                            //SWRegistro.Flush();
                        }
                        else
                        {
                            string Mensaje = "No acepto el precio enviado";

                            FalloComunicacion = true;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                            SWRegistro.Flush();
                        }
                        break;

                    case ComandoSurtidor.Predeterminar:

                        if (TramaRx[4] == 0)
                        {
                            FalloComunicacion = false;
                        }
                        else if (TramaRx[4] == 2)
                        {
                            string Mensaje = "No acepto Predeterminación enviado";

                            FalloComunicacion = true;
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                            SWRegistro.Flush();
                        }

                        break;

                    //case ComandoSurtidor.ObtenerVentaDinero:
                    //    PropiedadesCara[CaraEncuestada].TotalVenta =
                    //        Convert.ToDecimal(TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2") + TramaRx[6].ToString("X2")) /
                    //        PropiedadesCara[CaraEncuestada].FactorImporte;
                    //    break;

                    case ComandoSurtidor.ObtenerVentaVolumen:
                        ElementoInicial = 5;
                        Longitud = (TramaRx.Length - (ElementoInicial + 2));
                        PropiedadesCara[CaraEncuestada].Volumen = ObtenerValor16(ElementoInicial, Longitud);
                        break;

                    case ComandoSurtidor.ObtenerTotalizador:
                        ElementoInicial = 5;
                        Longitud = (TramaRx.Length - (ElementoInicial + 2));
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura = ObtenerValor16(ElementoInicial, Longitud);
                        break;


                    case ComandoSurtidor.ObtenerTotalizador_String:
                        ElementoInicial = 5;
                        Longitud = (TramaRx.Length - 4);
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura = ObtenerValor_String(ElementoInicial, Longitud);
                        break;

                    case ComandoSurtidor.ObtenerVentaVolumen_String:
                        ElementoInicial = 6;
                        int ElementoFinal = (TramaRx.Length - 4);
                        PropiedadesCara[CaraEncuestada].Volumen = Volumen_string(ElementoInicial, ElementoFinal);


                        break;


                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|AnalizarTrama: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void Re_Predeterminacion()
        {

            ProcesoEnvioComando(ComandoSurtidor.Detener);
            Thread.Sleep(500);
            ProcesoEnvioComando(ComandoSurtidor.Detener);

            if (ProcesoEnvioComando(ComandoSurtidor.Predeterminar))
            {
                string Mensaje = "Predeterminación Exitosa";

                FalloComunicacion = false;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|" + Mensaje);
                SWRegistro.Flush();
            }
            else
            {

                string Mensaje = "No acepto Predeterminación enviado por segunda vez";

                FalloComunicacion = true;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                SWRegistro.Flush();

                Evento_CancelarVenta(CaraEncuestada);

            }




        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void RecuperarEstado()
        {
            try
            {
                string Mensaje = "";

                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                    PropiedadesCara[CaraEncuestada].EstadoAnterior = PropiedadesCara[CaraEncuestada].Estado;

                //Asigna Estado
                if (TramaTx[4] == 1) // *** 1° Byte ***
                {
                    /*
                     * 1 bit BYTE codificado: 
                        Bit 0 - Modo de entrega = No, producto que fluye = No 
                        *** Bit 1 - modo de entrega = Sí, el producto fluye = Sí ***
                        *** Bit 2 - Modo de entrega = Sí, el producto fluye = No ***
                        *** Bit 3 - Modo de entrega = No, producto que fluye = Sí ***
                        Bit 4 - la impresora está ocupada 
                        Bit 5 - Posición UI metros correctos. Se establece cuando medidor no puede realizar comando solicitado 
                        debido al estado de cambio de usuario / botón por ejemplo, no se puede restablecer si C & C interruptor activado) 
                        Bit 6 - Error de metro 
                        Bit 7 - Establecer si el modo de C & C activada
                     */

                    int Bit = Convert.ToInt16(TramaRx[5]);

                    if ((Bit & 0x01) == 0x01)//Repos
                    {//Mensaje = "Bit 0 - Modo de entrega = No, producto que fluye = No ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Reposo;
                    }

                    if ((Bit & 0x02) == 0x02)//Abasteciendo o cargando 
                    { //Mensaje = "Bit 1 - modo de entrega = Sí, el producto fluye = Sí ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_En_Carga;
                    }

                    if ((Bit & 0x04) == 0x04)//Entrega pausada
                    { //Mensaje = " Bit 2 - Modo de entrega = Sí, el producto fluye = No  ";
                        //if (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.EMR3_PorAutorizar)

                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Carga_Pausada;
                    }

                    if ((Bit & 0x08) == 0x08)
                    { //Mensaje = " Bit 3 - Modo de entrega = No, producto que fluye = Sí  ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Fuga;
                    }

                    if ((Bit & 0x64) == 0x64)
                    { //Mensaje = "Bit 6 - Error de metro ";
                        PropiedadesCara[CaraEncuestada].Estado = EstadoCara.Error_EMR3;
                    }

                    //if ((Bit & 0x016) == 0x016)
                    //{//Mensaje = "Bit 4 - la impresora está ocupada";
                    //   PropiedadesCara[CaraEncuestada].Estado = EstadoCara.Impresora_ocupada;                   
                    //}

                    //if ((Bit & 0x32) == 0x32)
                    //{//Mensaje = "Bit 5 - Posición UI metros correctos. Se establece cuando medidor no puede realizar comando solicitado debido al estado de cambio de usuario / botón por ejemplo, no se puede restablecer si C & C interruptor activado) ";
                    // PropiedadesCara[CaraEncuestada].Estado = EstadoCara.Incorreta_Medicion;
                    //}                   

                    //if ((Bit & 0x128) == 0x128)
                    //{   //Mensaje = "Bit 7 - Establecer si el modo de C & C activada";
                    //    PropiedadesCara[CaraEncuestada].Estado = EstadoCara.C_C_Activado;
                    //}

                }

                if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.EMR3_Carga_Pausada)
                {

                    if (ProcesoEnvioComando(ComandoSurtidor.Estado_Delivery))
                    {
                        /**** 1° Byte ***
                        1-  Bit 0 - Error ATC 
                        2-  Bit 1 - error pulsador / codificador 
                        4-  Bit 2 - preset error 
                        8-  Bit 3 - Parada preestablecida. Se establece cuando la entrega se detuvo después de alcanzar el volumen preestablecido. 
                        16- Bit 4 - sin parar flujo (tiempo de espera) 
                        32- Bit 5 - solicitud de entrega de pausa 
                        64- Bit 6 - Solicitud de final de salida 
                        128 Bit 7 - la espera de autorización                
                       */

                        int Bit = Convert.ToInt16(TramaRx[5]);//se analiza el Primer Byte - Bytes 5 y 6 del RX 

                        if ((Bit & 0x1) == 0x1)
                        {
                            Mensaje = " Bit 0 - Error ATC  ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x2) == 0x2)
                        {
                            Mensaje = "Bit 1 - error pulsador / codificador ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x4) == 0x4)
                        {
                            Mensaje = "Bit 2 - preset error Entrego más de lo Programado";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x8) == 0x8)
                        {

                            Mensaje = "Bit 3 - Parada preestablecida. Se establece cuando la entrega se detuvo después de alcanzar el volumen preestablecido";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado && 
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.EMR3_Carga_Pausada )
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje + "PropiedadesCara[CaraEncuestada].Estado = " + PropiedadesCara[CaraEncuestada].Estado);
                                SWRegistro.Flush();
                            }
                            //enviar el comando de detencion ya se acompleto el valor programado 
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Preset_completo;
                        }

                        if ((Bit & 0x10) == 0x10)
                        {
                            Mensaje = "Bit 4 - sin parar flujo (tiempo de espera)";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x20) == 0x20)
                        {
                            Mensaje = "Bit 5 - solicitud de entrega de pausa";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Preset_Pausado;
                        }

                        if ((Bit & 0x40) == 0x40)
                        {
                            //Mensaje = "Bit 6 - Solicitud de final de salida";
                            //if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            //{
                            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                            //    SWRegistro.Flush();
                            //}
                        }

                        if ((Bit & 0x80) == 0x80)
                        {//Mensaje = "Bit 7 - la espera de autorización";

                            PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_PorAutorizar;

                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.EMR3_PorAutorizar)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado| Bit 7 - la espera de autorización");
                                SWRegistro.Flush();
                            }
                        }


                        Bit = Convert.ToInt16(TramaRx[6]);

                        /**** 2° Byte ***
                         1-  Bit 8 - Ticket de entrega está pendiente 
                             Bit 9 - flujo está activo 
                             Bit 10 - entrega está activa 
                             Bit 11 - preset activo neto es 
                             Bit 12 - gross preset está activo 
                             Bit 13 - ATC está activo 
                             Bit 14 - Entrega completa 
                         128 Bit 15 - error de entrega 
                         */

                        if ((Bit & 0x1) == 0x1)
                        {
                            Mensaje = "Bit 8 - Ticket de entrega está pendiente";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x02) == 0x02)
                        {
                            Mensaje = "Bit 9 - flujo está activo  ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }
                        if ((Bit & 0x4) == 0x4)
                        {
                            Mensaje = "Bit 10 - entrega está activa";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x8) == 0x8)
                        {
                            Mensaje = "Bit 11 - preset activo neto es ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }
                        if ((Bit & 0x10) == 0x10)
                        {
                            Mensaje = "Bit 12 - gross preset está activo ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x20) == 0x20)
                        {
                            Mensaje = "Bit 13 - ATC está activo ";

                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }

                        if ((Bit & 0x40) == 0x40)
                        {
                            Mensaje = "Bit 14 - Entrega completa ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != EstadoCara.EMR3_Entraga_completa)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                                PropiedadesCara[CaraEncuestada].Estado = EstadoCara.EMR3_Entraga_completa;
                            }
                        }

                        if ((Bit & 0x80) == 0x80)
                        {
                            Mensaje = "Bit 15 - error de entrega ";
                            if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + Mensaje);
                                SWRegistro.Flush();
                            }
                        }
                    }

                }


                //Almacena en archivo el estado actual del surtidor
                if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Estado|" + PropiedadesCara[CaraEncuestada].Estado.ToString());
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|RecuperarEstado: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region PROCESOS DE TOMA DE DECISIONES SEGÚN ESTADOS DE LA CARA

        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (PropiedadesCara[CaraEncuestada].Estado)
                {
                    #region EMR3_Reposo
                    case (EstadoCara.EMR3_Reposo):
                        //Informa cambio de estado

                        //oEvento_Predeterminar();//Borra solo para Pruebas.

                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado)
                        {
                            int IdManguera = 1;

                            if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                            {
                                Thread.Sleep(500); // tiempo de espera para que actualice datos de fin de venta volumen y Totalizador

                                ProcesoFindeVenta();
                                PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                            }


                            if (CaraEnReposo != null)
                            {
                                CaraEnReposo(CaraID, IdManguera);
                            }
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa cara en Espera. Manguera " + IdManguera);
                            SWRegistro.Flush();

                            if (ProcesoEnvioComando(ComandoSurtidor.Desautorizar))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Cara Desautorizada ");
                                SWRegistro.Flush();
                            }

                        }

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true || PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            LecturaAperturaCierre();

                        //ORedSurtidor.CambiarProductoAMangueras = true;
                        //Revisa si se tiene que hacer cambio de producto en alguna manguera de la cara
                        if (PropiedadesCara[CaraEncuestada].CambiarProductoAMangueras)// New DCF 2011-02-11
                        {
                            //Revisando en que grados hay que cambiar el producto
                            foreach (Grados OGrado in PropiedadesCara[CaraEncuestada].ListaGrados)
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

                                        if (NotificarCambioPrecioManguera != null)
                                        {
                                            NotificarCambioPrecioManguera(MangueraANotificar);
                                        }
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

                            PropiedadesCara[CaraEncuestada].CambiarProductoAMangueras = false;
                        }



                        break;
                    #endregion

                    #region EMR3_PorAutorizar
                    case EstadoCara.EMR3_PorAutorizar:

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
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            PropiedadesCara[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Informa cambio de estado sólo si la venta anterior ya fue liquidada
                        if (PropiedadesCara[CaraEncuestada].EstadoAnterior != PropiedadesCara[CaraEncuestada].Estado &&
                            PropiedadesCara[CaraEncuestada].EsVentaParcial == false)
                        {
                            PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta; //se asigna el grado que despacha

                            if (ProcesoTomaLectura())
                            {
                                int IdProducto =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto;
                                int IdManguera =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;
                                string Lectura =
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura.ToString("N3");

           

                                if (AutorizacionRequerida != null)
                                {
                                    // -- Modificado 2012.04.23-0901
                                    SWRegistro.WriteLine(
                                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                                    DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                                     " |" + CaraID + "|Antes de Enviar oEventos.RequerirAutorizacion. - Grado "
                                        + PropiedadesCara[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                        IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                    SWRegistro.Flush();// -- Modificado 2012.04.23-0901

                                    AutorizacionRequerida(CaraID, IdProducto, IdManguera, Lectura,"");
                                }

                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador para Lectura Inicial Venta");
                                SWRegistro.Flush();
                            }

                        }
                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (PropiedadesCara[CaraEncuestada].AutorizarCara == true)
                        {
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;

                            string strLecturasVolumen =
                                PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");


                            if (LecturaInicialVenta != null)
                            {
                                LecturaInicialVenta(CaraID, strLecturasVolumen);
                            }

                            //Loguea Evento de envio de lectura
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informar Lectura Inicial de Venta: " +
                                strLecturasVolumen);
                            SWRegistro.Flush();

                            int Reintenos = 1;
                            do
                            {
                                if (!ProcesoEnvioComando(ComandoSurtidor.Autorizar))
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondió comando de Autorizar Despacho");
                                    SWRegistro.Flush();
                                }
                                else
                                {
                                    PropiedadesCara[CaraEncuestada].AutorizarCara = false;

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Comando Autorizacion enviado con exito");
                                    SWRegistro.Flush();


                                }
                                Thread.Sleep(100);//DCF para actualizar estado
                                ProcesoEnvioComando(ComandoSurtidor.Estado);
                                Reintenos++;
                            } while (PropiedadesCara[CaraEncuestada].Estado != EstadoCara.EMR3_En_Carga &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.EMR3_Pausado &&
                                PropiedadesCara[CaraEncuestada].Estado != EstadoCara.EMR3_Carga_Pausada && Reintenos <= 2);//No Autorizar con manguera colgada:  PropiedadesCara[CaraEncuestada].Estado != EstadoCara.WayneReposo  Wayne 2011.06.03-1134


                        }
                        break;
                    #endregion

                    #region EMR3_En_Carga
                    case (EstadoCara.EMR3_En_Carga):
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
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                PropiedadesCara[CaraEncuestada].TomarLecturaCierreTurno = false;
                                if (CancelarProcesarTurno != null)
                                {
                                    CancelarProcesarTurno(CaraID, MensajeErrorLectura, EstadoTurno);
                                }
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
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
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = true;

                        string strVolumen = "";
                        string strTotalVenta = "";


                        if (ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaVolumen))
                        {
                            strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                        }
                        else
                        {
                            ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaVolumen_String);

                            strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                        }

                        PropiedadesCara[CaraEncuestada].TotalVenta = PropiedadesCara[CaraEncuestada].PrecioVenta * PropiedadesCara[CaraEncuestada].Volumen;

                        //Reporta los valores de parciales de despacho Calculado               
                        strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");



                        if (VentaParcial != null)
                        {
                            VentaParcial(CaraTmp, strTotalVenta, strVolumen);
                        }


                        break;
                    #endregion


                    case EstadoCara.EMR3_Carga_Pausada:
                        break;


                    case EstadoCara.EMR3_Fuga:
                        break;


                    case EstadoCara.WayneDespachoAutorizado:
                        break;


                    case EstadoCara.EMR3_Preset_completo:

                        if (PropiedadesCara[CaraEncuestada].EsVentaParcial)
                        {
                            ProcesoFindeVenta();
                            PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                            Tecla_Fin();

                        }
                        break;

                    default:

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Estado Indeterminado: " + PropiedadesCara[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|TomarAccion: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private bool CambiarPreciosEnGrado(Grados grado)
        {
            bool CambioPrecios = false;

            try
            {
                //Variable que indica qué precio cambiar
                //bool PrecioNivel1 = new bool();
                int Reintentos = 0;

                //Si hay cambio de precio pendiente, lo aplica
                for (int i = 0; i <= PropiedadesCara[CaraEncuestada].ListaGrados.Count - 1; i++)
                {
                    if (PropiedadesCara[CaraEncuestada].ListaGrados[i].NoGrado == grado.NoGrado)
                    {
                        PropiedadesCara[CaraEncuestada].GradoCara = i;


                        //PrecioNivel1 = true;
                        Reintentos = 0;
                        do
                        {

                            if (ProcesoEnvioComando(ComandoSurtidor.EstalecerPrecio))
                            {
                                if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 ==
                                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio aceptado por cara. Grado " +
                                        PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                                    SWRegistro.Flush();

                                    CambioPrecios = true;
                                    break;
                                }
                                else
                                {
                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|No se pudo establecer nuevo Precio Nivel 1: Precio del Surtidor: " +
                                               PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioSurtidorNivel1 +
                                               " - Precio Requerido: " + PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1 + " - Reintentos: " + Reintentos);
                                    SWRegistro.Flush();

                                    //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                    int Manguera = PropiedadesCara[CaraEncuestada].ListaGrados[i].MangueraBD;
                                    double Precio = Convert.ToDouble(PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1);

                                    if (CambioPrecioFallido != null)
                                    {
                                        CambioPrecioFallido(Manguera, Precio);
                                    }
                                }


                            }
                            else
                            {
                                Reintentos += 1;

                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando Establecer Precio");
                                SWRegistro.Flush();

                                //REPORTANDO EL CAMBIO DE PRECIO FALLIDO
                                int Manguera = PropiedadesCara[CaraEncuestada].ListaGrados[i].MangueraBD;
                                double Precio = Convert.ToDouble((PropiedadesCara[CaraEncuestada].ListaGrados[i].PrecioNivel1));
                                if (CambioPrecioFallido != null)
                                {
                                    CambioPrecioFallido(Manguera, Precio);
                                }
                            }

                        } while (Reintentos <= 3);

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
        } //New

        private bool ProcesoTomaLectura()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador_String))
                {
                    return true;

                }
                else
                {

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador..");
                    SWRegistro.Flush();

                    //Realizar la consulta para obtener el totalizador en formato string
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                    {

                        return true;
                    }
                    else
                        return false;
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoTomaLectura: " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //REALIZA PROCESO DE FIN DE VENTAF
        private void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                PropiedadesCara[CaraEncuestada].Volumen = 0;
                PropiedadesCara[CaraEncuestada].TotalVenta = 0;
                PropiedadesCara[CaraEncuestada].PrecioVenta = 0;

                //Si el grado de fin de venta no corresponde con el de inicio de venta, quiere decir que la lectura inicial esta mal tomada
                if (PropiedadesCara[CaraEncuestada].GradoCara != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 20110117
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia|Grado autorizado: " + PropiedadesCara[CaraEncuestada].GradoCara +
                        " - Grado que vendio: " + PropiedadesCara[CaraEncuestada].GradoVenta);
                    SWRegistro.Flush();
                }

                PropiedadesCara[CaraEncuestada].GradoCara = PropiedadesCara[CaraEncuestada].GradoVenta; //Actualizo el grado que despacho

                int conteo = 0;
                difer = 0;

                do
                {
                    if (!ProcesoTomaLectura())
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                        SWRegistro.Flush();

                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta = 0; //DCF

                        break;
                    }
                    else
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;

                    conteo++;

                    difer = (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].Lectura -
                           PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta);

                    if (difer < 0)
                    {
                        Thread.Sleep(200); //para que refresque la lectura para terpel
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|ERROR| ** Totalizador Entregado menor que Li ** LF = " +
                              PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura);
                        SWRegistro.Flush();
                    }

                } while ((difer) < 0 && conteo < 3);

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                {
                    PropiedadesCara[CaraEncuestada].PrecioVenta =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].PrecioSurtidorNivel1;
                }

                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No acepto comando de obtencion de Precio en Final de Venta");
                    SWRegistro.Flush();
                }


                int ciclo = 0;


                do
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaVolumen))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Volumen en Fin de Venta " + PropiedadesCara[CaraEncuestada].Volumen.ToString("N2"));
                        SWRegistro.Flush();
                    }
                    else
                    {
                        ProcesoEnvioComando(ComandoSurtidor.ObtenerVentaVolumen_String);

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Volumen_String en Fin de Venta " + PropiedadesCara[CaraEncuestada].Volumen.ToString("N2"));
                        SWRegistro.Flush();

                    }

                    ciclo += 1;

                } while (PropiedadesCara[CaraEncuestada].Volumen == 0 && (ciclo <= 3));

                if (PropiedadesCara[CaraEncuestada].Volumen == 0)
                {
                    PropiedadesCara[CaraEncuestada].Volumen = difer;

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|se Toma Volumen Calculado por diferencia de lecturas.  = " + difer);
                    SWRegistro.Flush();

                }

                if (difer < 0 && PropiedadesCara[CaraEncuestada].Volumen != 0) //DCF 18/04/2013 indica que la lectura final es menor a la inicial y se recalcula con el volumen despachado + lectura Inicial.
                {
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                        (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta +
                        PropiedadesCara[CaraEncuestada].Volumen);

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Inconsistencia| **** Se Calcula Lectura Final = LecturaInicialVenta (" + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta + ")"
                        + " + Volumen (" + PropiedadesCara[CaraEncuestada].Volumen + ") = " + PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta);
                    SWRegistro.Flush();
                }

                ////Calcula la correspondencia entre el Volumen, el Precio y el Importe //DCF
                decimal TotalVentaCalculada = PropiedadesCara[CaraEncuestada].Volumen *
                    PropiedadesCara[CaraEncuestada].PrecioVenta; //

                PropiedadesCara[CaraEncuestada].TotalVenta = TotalVentaCalculada;

                //se genera un error en Bolivia no entrega totalizador Actualizado. se pregunta hasta 3 veces para que actualice la LF si entrega dato de venta Volumen 
                int contLF = 0;

                if (PropiedadesCara[CaraEncuestada].Volumen != 0 &&
                   (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta))
                {

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "| ** - No entrego LF Actualizada - **");
                    do
                    {
                        Thread.Sleep(1500);

                        ProcesoTomaLectura();

                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta =
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Lectura;


                        contLF++;

                    } while (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta && contLF < 3);
                    //
                }

                //Para Wayne Volumen_VenteAnterior 26/08/2013
                if ((PropiedadesCara[CaraEncuestada].Volumen != 0) &&
                    ((PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta !=
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta)
                    || PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Volumen_Venta_Anterior !=
                    PropiedadesCara[CaraEncuestada].Volumen))
                {
                    //Almacena los valores en las variables requerida por el Evento
                    string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                    string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                    string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                    string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                    string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                    byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                    int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;

                    //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;

                    //Loguea evento Fin de Venta
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                        " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                        " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                    SWRegistro.Flush();

                    String PresionLLenado = "0";

                    if (VentaFinalizada != null)
                    {
                        VentaFinalizada(CaraID, strTotalVenta, strPrecio, strLecturaFinalVenta,
                                strVolumen, bytProducto.ToString(), IdManguera, PresionLLenado, strLecturaInicialVenta);
                    }

                    ////if (PropiedadesCara[CaraEncuestada].GradoVentaInicial != PropiedadesCara[CaraEncuestada].GradoVenta) //DCF 27-07-2011 
                    ////{//Se asegura obtener siempre la lectura Inicial de volumen, para corregir el error de lecturas, en caso que el grado autorizado no sea el que despacho
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta =
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta;

                    //Almaceno el Dato de volumen para compararlo con el volumen siguiente
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].Volumen_Venta_Anterior = PropiedadesCara[CaraEncuestada].Volumen;

                }
                else
                {
                    if (VentaInterrumpidaEnCero != null)
                    {
                        VentaInterrumpidaEnCero(CaraID);
                    }
                    PropiedadesCara[CaraEncuestada].EsVentaParcial = false;
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Venta en CERO");
                    SWRegistro.Flush();




                    //DCF 

                    if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta ==
                   PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta) ////Venta en Cero si LF = LI, indicado Por wal 
                    {
                        string strTotalVenta = PropiedadesCara[CaraEncuestada].TotalVenta.ToString("N3");
                        string strPrecio = PropiedadesCara[CaraEncuestada].PrecioVenta.ToString("N3");
                        string strLecturaFinalVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaFinalVenta.ToString("N3");
                        string strVolumen = PropiedadesCara[CaraEncuestada].Volumen.ToString("N3");
                        string strLecturaInicialVenta = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].LecturaInicialVenta.ToString("N3");
                        byte bytProducto = Convert.ToByte(PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].IdProducto);
                        int IdManguera = PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoVenta].MangueraBD;


                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Seguimiento|LecturaInicialVenta = LecturaFinalVenta. Importe: " + strTotalVenta +
                          " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                          " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                        SWRegistro.Flush();
                    }
                }







            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoFindeVenta: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void LecturaAperturaCierre()
        {
            try
            {
                bool TomaLecturasExitoso = true;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();

                System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();

                foreach (Grados Grado in PropiedadesCara[CaraEncuestada].ListaGrados) //Recorre las mangueras de la cara encuestada
                {
                    PropiedadesCara[CaraEncuestada].GradoCara = Grado.NoGrado; //DCF para tomar los totalizadores de todas las mangueras 

                    if (ProcesoTomaLectura())
                    {
                        //Arma Arreglo de lecturas
                        ArrayLecturas.Add(Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura) + "|" +
                            Convert.ToString(PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));

                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Arma Lecturas para turno. Manguera " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[Grado.NoGrado].Lectura);

                        SWRegistro.Flush();

                        //Cambia el precio si es apertura de turno
                        if (PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Inicia cambio de precios");
                            SWRegistro.Flush();

                            ProcesoCambioPrecio();
                        }
                    }
                    else
                    {
                        TomaLecturasExitoso = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando de obtener Totalizador para Lectura Inicial/Final de Turno. Grado: " + Grado.NoGrado);
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
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Finales de turno");
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
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Informa Lecturas Iniciales de turno ");
                        SWRegistro.Flush();
                        PropiedadesCara[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|LecturaAperturaCierre: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void ProcesoCambioPrecio()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Cambio Precio Grado " +
                    PropiedadesCara[CaraEncuestada].GradoCara + " - Precio Enviado a la Consola : " +
                    PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                SWRegistro.Flush();

                if (ProcesoEnvioComando(ComandoSurtidor.EstalecerPrecio))
                {
                    ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio);

                    if (PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioSurtidorNivel1 ==
                        PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio aceptado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara + " - Precio: " +
                            PropiedadesCara[CaraEncuestada].ListaGrados[PropiedadesCara[CaraEncuestada].GradoCara].PrecioNivel1);
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Precio rechazado por cara. Grado " +
                            PropiedadesCara[CaraEncuestada].GradoCara);
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Fallo|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|ProcesoCambioPrecio: " + Excepcion);
                SWRegistro.Flush();
            }
        }

        #endregion

        #region EVENTOS DE LA CLASE

        public void Evento_VentaAutorizada(byte Cara, string Precio, string ValorProgramado, byte TipoProgramacion, string Placa, int MangueraProgramada, bool EsVentaGerenciada, string guid, Decimal PresionLLenado)
        {
            try
            {

                // -- Modificado 2012.04.23-0901
                SWRegistro.WriteLine(
                DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                 " |" + Cara + "|Recibe oEvento_VentaAutorizada....");
                SWRegistro.Flush();// -- Modificado 2012.04.23-0901


                CaraTmp = ConvertirCaraBD(Cara);

                if (PropiedadesCara.ContainsKey(CaraTmp))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraTmp + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    PropiedadesCara[CaraTmp].AutorizarCara = true; //se activa sin abrir turno ???

                    //Valor a programar
                    PropiedadesCara[CaraTmp].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    PropiedadesCara[CaraTmp].PrecioVenta = Convert.ToDecimal(Precio);

                    PropiedadesCara[CaraTmp].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (PropiedadesCara[CaraTmp].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            PropiedadesCara[CaraTmp].PredeterminarImporte = false;
                            PropiedadesCara[CaraTmp].PredeterminarVolumen = true;
                        }
                        else
                        {
                            PropiedadesCara[CaraTmp].PredeterminarImporte = true;
                            PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        PropiedadesCara[CaraTmp].PredeterminarImporte = false;
                        PropiedadesCara[CaraTmp].PredeterminarVolumen = false;
                    }
                }

                else
                {
                    // -- Modificado 2012.04.23-0901
                    SWRegistro.WriteLine(
                    DateTime.Now.Day.ToString().PadLeft(2, '0') + "/" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "/" +
                    DateTime.Now.Year.ToString().PadLeft(4, '0') + " " +
                    DateTime.Now.Hour.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Minute.ToString().PadLeft(2, '0') + ":" +
                    DateTime.Now.Second.ToString().PadLeft(2, '0') + "." + DateTime.Now.Millisecond.ToString().PadLeft(3, '0') +
                     " |" + Cara + "|La Cara está Fuera de la red de Surtidores..");
                    SWRegistro.Flush();// -- Modificado 2012.04.23-0901

                }

            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEvento_VentaAutorizada: " + Excepcion);
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

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                //PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                //    (Grados[PropiedadesCara[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1)/
                                //    PropiedadesCara[CaraEncuestada].MultiplicadorPrecioVenta; //DCF para menejo de precio con 5 digitos

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1); //D

                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    (Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2);
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }

                        }

                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;



                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= PropiedadesCara[CaraTmp].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;


                                PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[PropiedadesCara[CaraTmp].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaAperturaTurno = true;
                            }


                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Inconsistencia|fuera de red de surtidores. Evento: oEvento_TurnoAbierto");
                            SWRegistro.Flush();
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

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;

                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        CaraTmp = ConvertirCaraBD(CaraLectura);//DCF
                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (PropiedadesCara.ContainsKey(CaraTmp))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            PropiedadesCara[CaraTmp].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (PropiedadesCara[CaraTmp].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                PropiedadesCara[CaraTmp].TomarLecturaCierreTurno = true;
                            }
                        }
                        else
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraLectura + "|Error|Fuera de red de surtidores. Evento: oEvento_TurnoCerrado");
                            SWRegistro.Flush();
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
        public void Evento_FinalizarVentaPorMonitoreoCHIP(byte Cara)
        {
            try
            {
                CaraTmp = ConvertirCaraBD(Cara); //DCF
                if (PropiedadesCara.ContainsKey(CaraTmp))
                {
                    PropiedadesCara[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(CaraTmp) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|oEVento_FinalizarVentaPorMonitoreoCHIP: " + Excepcion);
                SWRegistro.Flush();
            }

        }
        public void Evento_ProgramarCambioPrecioKardex(ColMangueras mangueras)
        {
            //Evento que manda a cambiar el producto y su respectivo precio en las mangueras
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
                                SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD +
                                    "|Evento| Recibe evento para Cambio Precio Kardex. Manguera: " + OGrado.MangueraBD +
                                    " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
                                SWRegistro.Flush();
                            }
                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEventos_ProgramarCambioPrecioKardex: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_Predeterminar(byte Cara, string ValorProgramado, byte TipoProgramacion)
        {
            try
            {
                bool predetermina = true;

                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Evento_Predeterminar: " +
                           PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                SWRegistro.Flush(); //Borra


                //  if (PropiedadesCara[CaraEncuestada].Estado == EstadoCara.EMR3_PorAutorizar)
                Evento_CancelarVenta(CaraEncuestada);           



                PropiedadesCara[CaraEncuestada].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);
                if (PropiedadesCara[CaraEncuestada].ValorPredeterminado == 0)
                    return;

                while (predetermina)
                {
                    if (ProcesoEnvioComando(ComandoSurtidor.Predeterminar))
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|Predeterminacion exitosa. Volumen: " +
                            PropiedadesCara[CaraEncuestada].ValorPredeterminado);
                        SWRegistro.Flush();

                        if (ProcesoEnvioComando(ComandoSurtidor.Desautorizar))
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Cara Desautorizada en Predeterminar Venta");
                            SWRegistro.Flush();
                        }
                        
                        predetermina = false;

                        Thread.Sleep(100);
                        this.StartSale();

                    }
                    else
                    {

                        Evento_CancelarVenta(CaraEncuestada);
                        if (ProcesoEnvioComando(ComandoSurtidor.Predeterminar))
                        {
                            predetermina = false;

                        }
                       

                    }
                }

            
            }
            catch (Exception Excepcion)
            {


                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|Evento_Predeterminar: " + Excepcion);
                SWRegistro.Flush();
            }


        }
        public void Tecla_Fin()
        {
            //tecla FIN
            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'S', (byte)'u', 0x01, (byte)(CRC), 0X7E };

            CalcularCRC(TramaTx);
            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'S', (byte)'u', 0x01, (byte)(CRC), 0X7E };

            PuertoCom.Write(TramaTx, 0, TramaTx.Length);

        }


        public void SendKey(KeysDevice KeyCommand)
        {
            byte key = (byte) KeyCommand;

            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'S', (byte)'u', key, (byte)(CRC), 0X7E };

            CalcularCRC(TramaTx);
            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'S', (byte)'u', key, (byte)(CRC), 0X7E };

            PuertoCom.Write(TramaTx, 0, TramaTx.Length);
            Thread.Sleep(500);
        }

       

        public void StartDevice()
        {
            //Iniciar Dispositivo
            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'O', 0X01, 0X01, (byte)(CRC), 0X7E };
            CalcularCRC(TramaTx);

            TramaTx = new byte[8] { 0x7E, 0X01, 0XFF, (byte)'O', 0X01, 0X01, (byte)(CRC), 0X7E };

            PuertoCom.Write(TramaTx, 0, TramaTx.Length);

            Thread.Sleep(5000);
        }


        public void StartSale()
        {
            StartDevice();
            SendKey(KeysDevice.Next);
            SendKey(KeysDevice.Enter);
            SendKey(KeysDevice.Next);
            SendKey(KeysDevice.Enter);            
        }


        public void Evento_CancelarVenta(byte Cara)
        {
            // autoriza
            TramaTx = new byte[8] { 0x7E, Cara, 0XFF, (byte)'O', 0X06, 0x01, (byte)(CRC), 0X7E };
            CalcularCRC(TramaTx);
            TramaTx = new byte[8] { 0x7E, Cara, 0XFF, (byte)'O', 0X06, 0x01, (byte)(CRC), 0X7E };
            PuertoCom.Write(TramaTx, 0, TramaTx.Length);

            Thread.Sleep(200);

            //desautorizo para cancelar el procerso 
            TramaTx = new byte[7] { 0x7E, Cara, 0XFF, (byte)'O', 0X03, (byte)(CRC), 0X7E };
            CalcularCRC(TramaTx);
            TramaTx = new byte[7] { 0x7E, Cara, 0XFF, (byte)'O', 0X03, (byte)(CRC), 0X7E };



            PuertoCom.Write(TramaTx, 0, TramaTx.Length);

            Thread.Sleep(500);

            // RecibirInformacion();


            //Analizo el RX si se dio la desautorizacion 

            //if (TramaRx[4] == 0)
            //{
            //    string Mensaje = "Evento_AnularDespacho OK";

            //    FalloComunicacion = true;
            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Proceso|" + Mensaje);
            //    SWRegistro.Flush();
            //}
            //else if (TramaRx[4] == 2)
            //{

            //    PuertoCom.Write(TramaTx, 0, TramaTx.Length);

            //    string Mensaje = "Evento_AnularDespacho Fallido";

            //    FalloComunicacion = true;
            //    SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Error|" + Mensaje);
            //    SWRegistro.Flush();
            //}
        }


        public void Evento_CerrarProtocolo()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Evento|Recibe evento de detencion de Protocolo");
                SWRegistro.Flush();
                CondicionCiclo = false;
            }
            catch (Exception Excepcion)
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraID + "|Excepcion|oEventosCerrarProtocolo: " + Excepcion);
                SWRegistro.Flush();
            }
        }
        public void Evento_InactivarCaraCambioTarjeta(byte Cara, string Puerto) { }

        public void Evento_FinalizarCambioTarjeta(byte Cara) { }

        #endregion
    }
}
