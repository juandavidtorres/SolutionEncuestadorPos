using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;            //Para manejo del Timer
using System.IO;                //Para manejo de Archivo de Texto
using System.IO.Ports;          //Para manejo del Puerto
using System.Threading;         //Para manejo del Timer
using System.Windows.Forms;     //Para alcanzar la ruta de los ejecutables

namespace gasolutions.Protocolos.Wayne
{
    public class Wayne
    {
        #region DECLARACION DE VARIABLES Y DEFINICIONES

        Dictionary<byte, RedSurtidor> EstructuraRedSurtidor;        //Diccionario donde se almacenan las Caras y sus propiedades

        public enum ComandoSurtidor
        {

            //Mensajes de Pedido de Informacion
            Estado,
            AutorizarDespacho,
            Autorizar,
            ObtenerPrecio,
            ObtenerVentaDinero,
            ObtenerVentaVolumen,
            AsignarPrecio,
            Predeterminar,
            CancelarPredeterminacion,
            ObtenerTotalizador,
            Totalizador1,
            Totalizador2,
            Totalizador3,
            Totalizador4,
            FinDeVenta,
            ObtenerDespacho,
            EstablecerPrecio


        }   //Define los posibles COMANDOS que se envian al Surtidor
        ComandoSurtidor ComandoCaras;

        byte CaraEncuestada;             //Cara que se esta ENCUESTANDO
        int TimeOut;                    //Tiempo de espera de respuesta del surtidor
        int BytesEsperados;             //Declara la cantidad de bytes esperados por Comando
        int eco;                        //Variable que toma un valor diferente de 0, dependiendo si la interfase devuelve ECO
        bool TramaEco;                  //Bandera que indica si dentro de la trama respuesta viene eco o no

        /*Arreglo que almacena el tipo de fallo de Comunicacion: Error en Integridad de Datos o Error de Comunicacion*/
        bool FalloComunicacion;      //Almacena el tipo de fallo de comunicacion        

        byte[] TramaRx = new byte[1];   //Almacena la TRAMA RECIBIDA
        byte[] TramaTx = new byte[1];   //Almacena la TRAMA A ENVIAR       

        //CREACION DE LOS OBJETOS A SER UTILIZADOS POR LA CLASE
        SerialPort PuertoCom = new SerialPort();                        //Definicion del objeto que controla el PUERTO DE LOS SURTIDORES
        SharedEventsFuelStation.CMensaje oEventos;                      //Controla la comunicacion entre las aplicaciones por medio de eventos

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


        byte CaraWayne;
        byte Manguera;

        int ContaTot;

        string Totalizador1;
        string Total1;
        string Total2;

        decimal VentaPrecio;
        decimal VentaDinero;
        decimal VentaVolumen;

        bool NoEnvioComando;
        int MangueraEncuestada;

        int ContaListaGrado = 0; //pRUEBA 

        string strValorImporte;
        string strValorVolumen;
        int MangueraBDD;
        int Reintentos;
        #endregion

        #region PUNTO DE ARRANQUE
        //PUNTO DE ARRANQUE DE LA CLASE
        public Wayne(string Puerto, Dictionary<byte, RedSurtidor> EstructuraCaras, bool Eco)
        {
            try
            {
                //Crea archivo para almacenar las tramas de transmisión y recepción (Comunicación con Surtidor)
                ArchivoTramas = DateTime.Today.ToString("yyyymmdd") + "-Wayne-Tramas.(" + Puerto + ").txt";
                SWTramas = File.AppendText(ArchivoTramas);

                //Crea archivo para almacenar inconsistencias en el proceso logico
                Archivo = DateTime.Today.ToString("yyyymmdd") + "- Wayne-Sucesos(" + Puerto + ").txt";
                SWRegistro = File.AppendText(Archivo);

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine("===================|==|======|=========================================");
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Protocolo. Wayne 2010.03.19 -1000");
                SWRegistro.Flush();

                //Instancia los eventos disparados por la aplicacion cliente
                Type t = Type.GetTypeFromProgID("SharedEventsFuelStation.CMensaje");
                oEventos = (SharedEventsFuelStation.CMensaje)Activator.CreateInstance(t);
                oEventos.VentaAutorizada += new SharedEventsFuelStation.__CMensaje_VentaAutorizadaEventHandler(oEvento_VentaAutorizada);
                oEventos.TurnoAbierto += new SharedEventsFuelStation.__CMensaje_TurnoAbiertoEventHandler(oEvento_TurnoAbierto);
                oEventos.TurnoCerrado += new SharedEventsFuelStation.__CMensaje_TurnoCerradoEventHandler(oEvento_TurnoCerrado);
                //oEventos.ProgramarCambioPrecioKardex += new SharedEventsFuelStation.__CMensaje_ProgramarCambioPrecioKardexEventHandler(oEventos_ProgramarCambioPrecioKardex);
                oEventos.FinalizarVentaPorMonitoreoCHIP += new SharedEventsFuelStation.__CMensaje_FinalizarVentaPorMonitoreoCHIPEventHandler(oEventos_FinalizarVentaPorMonitoreoCHIP);
                //oEventos.CerrarProtocolo += new SharedEventsFuelStation.__CMensaje_CerrarProtocoloEventHandler(oEventos_CerrarProtocolo);

                //Si el puerto no esta abierto, se configura, inicializa y se deja listo para la operacion
                if (!PuertoCom.IsOpen)
                {


                    PuertoCom.PortName = Puerto;
                    PuertoCom.BaudRate = 9600;
                    PuertoCom.DataBits = 8;
                    PuertoCom.StopBits = StopBits.One;
                    PuertoCom.Parity = Parity.Odd;
                    PuertoCom.Open();
                    PuertoCom.DiscardInBuffer();
                    PuertoCom.DiscardOutBuffer();

                }

                //EstructuraRedSurtidor es la referencia con la que se va a trabajar
                EstructuraRedSurtidor = new Dictionary<byte, RedSurtidor>();
                EstructuraRedSurtidor = EstructuraCaras;

                foreach (RedSurtidor oCara in EstructuraRedSurtidor.Values)
                {
                    foreach (Grados oGrado in EstructuraRedSurtidor[oCara.Cara].ListaGrados)
                        SWRegistro.WriteLine(DateTime.Now + "|" + oCara.Cara + "|Inicio|Grado: " + oGrado.NoGrado + " - Manguera: " + oGrado.MangueraBD +
                            " - IdProducto: " + oGrado.IdProducto + " - Precio: " + oGrado.PrecioNivel1);
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
                string MensajeExcepcion = "Excepción en el Constructor de la Clase Wayne";
                SWRegistro.WriteLine(DateTime.Now + "|0|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //CICLO INFINITO DE RECORRIDO DE LAS CARAS (REEMPLAZO DEL TIMER)
        private void CicloCara()
        {
            try
            {

                //Variable para garantizar el ciclo infinito
                bool CondicionCiclo = true;

                //Escribe encabezado en archivo de Inconsistencias
                SWRegistro.WriteLine(DateTime.Now + "|0|Inicio|Inicia ciclo de encuesta a " + EstructuraRedSurtidor.Count + " caras");
                SWRegistro.Flush();

                //Ciclo Infinito
                while (CondicionCiclo)
                {
                    //Ciclo de recorrido por las caras
                    foreach (RedSurtidor ORedCaras in EstructuraRedSurtidor.Values)
                    {
                        //Si la cara está activa, realizar proceso de encuesta
                        if (ORedCaras.Activa == true)
                        {
                            CaraEncuestada = ORedCaras.Cara;
                            //Si el proceso de enviar el comando de Estado resulto exitoso, Toma la Accion necesaria
                            if (ProcesoEnvioComando(ComandoSurtidor.Estado))
                                TomarAccion();
                        }
                        Thread.Sleep(20);
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CicloCara: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        #endregion

        #region CONSTRUCCIÓN, ENVÍO Y RECEPCIÓN DE TRAMAS DE COMUNICACIÓN

        //EJECUTA CICLO DE ENVIO DE COMANDOS (REINTENTOS)
        private bool ProcesoEnvioComando(ComandoSurtidor ComandoaEnviar)
        {
            try
            {
                ComandoCaras = ComandoaEnviar;

                //Variable que indica el maximo numero de reintentos
                int MaximoReintento = 2;

                //Variable que controla la cantidad de reintentos fallidos de envio de comandos
                 Reintentos = 0;

                //Se inicializa la bandera de control de fallo de comunicación
                FalloComunicacion = false;



                //Arma la trama de Transmision
                ArmarTramaTx();


                // hacer salto despues de salir totalizador 1 y totalizador 2 + Obtener los valores de venta 
                //if ((ComandoCaras != ComandoSurtidor.ObtenerTotalizador) && (ComandoCaras != ComandoSurtidor.ObtenerDespacho));

                if (ComandoCaras != ComandoSurtidor.ObtenerTotalizador)
                    if (ComandoCaras != ComandoSurtidor.ObtenerDespacho)
                        if (ComandoCaras != ComandoSurtidor.EstablecerPrecio)
                            if (ComandoCaras != ComandoSurtidor.ObtenerPrecio)
                            {
                                {
                                    {
                                        {
                                            do
                                            {
                                                EnviarComando();
                                                //Analiza la información recibida si se espera respuesta del Surtidor
                                                //if (BytesEsperados > 0)
                                                //{
                                                RecibirInformacion();
                                                Reintentos += 1;
                                                //}
                                            }
                                            while (FalloComunicacion == true && Reintentos < MaximoReintento);
                                        }
                                    }
                                }
                            }
                //Se loguea si hubo el maximo numero de reintentos y no se recibio respuesta satisfactoria
                if (FalloComunicacion)
                {
                    //Envía ERROR EN TOMA DE LECTURAS, si NO hay comunicación con el surtidor
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                    {
                        string MensajeErrorLectura = "Error en Comunicacion con Surtidor";
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            bool EstadoTurno = false;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                            oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Inciales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                        {
                            bool EstadoTurno = true;
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                            oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Finales." + MensajeErrorLectura);
                            SWRegistro.Flush();
                        }
                        EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                    }

                    if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Perdida de comunicacion en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = true;
                    }

                    //Regresa el parámetro FALSE si hubo error en la trama o en la comunicación con el surtidor
                    return false;
                }
                else
                {
                    if (EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Se reestablece comunciación con surtidor en " + ComandoaEnviar);
                        SWRegistro.Flush();
                        EstructuraRedSurtidor[CaraEncuestada].FalloReportado = false;
                    }
                    //Regresa el parámetro TRUE si no hubo error alguno
                    return true;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de el método ProcesoEnvioComando: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
                return false;
            }
        }

        //ARMA LA TRAMA A SER ENVIADA
        private void ArmarTramaTx()
        {
            try
            {
                //string strPrecio;
                //string strValorImporte;
                //string strValorVolumen;

                switch (ComandoCaras)
                {

                    case ComandoSurtidor.EstablecerPrecio:
                        #region ComandoSurtidor.EstablecerPrecio:
                        TimeOut = 200;
                        AsignacionCaraClaseIII();

                        foreach (Grados oGrado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                        {
                            string strPrecioHex = Convert.ToInt32((EstructuraRedSurtidor[CaraEncuestada].ListaGrados[oGrado.NoGrado].PrecioNivel1 *
                                        EstructuraRedSurtidor[CaraEncuestada].FactorPrecio)).ToString("X2").PadLeft(4, '0');
                            byte PrecioH = Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 4, 2), 16);
                            byte PrecioL = Convert.ToByte(strPrecioHex.Substring(strPrecioHex.Length - 2, 2), 16);
                            Manguera = oGrado.NoGrado;

                            TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x01, 0x00, Manguera, 0x00, PrecioL, 0x00, PrecioH, 0x00, 0xFF };
                            ComplementoByte();

                            do
                            {                           
                            EnviarComando();
                            ContaTot = 1;
                            RecibirInformacion();
                            ContaTot = 0;
                            //EstructuraRedSurtidor[CaraEncuestada].GradoCara
                            }
                           while (FalloComunicacion == true && Reintentos < 2);

                              
                            

                        }

                        break;

                        #endregion

                    case ComandoSurtidor.ObtenerTotalizador:
                        #region Obtener los totalizadors de la Cara - Manguera
                        Reintentos = 0;
                        TimeOut = 200;
                        NoEnvioComando = true;
                        //case ComandoSurtidor.Totalizador1:
                        AsignacionCaraClaseIII();
                        AsignarMangueraClaseII();

                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x02, 0x00, Manguera, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };

                        ComplementoByte();
                        do
                        {
                            EnviarComando();
                            ContaTot = 1;
                            RecibirInformacion();
                            Reintentos += 1;
                        }
                        while (FalloComunicacion == true && Reintentos < 2);

                        //Almacenar el Totalizador 1
                        Total1 = (TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"));

                        // se debe enviar el comando ComandoSurtidor.Totalizador2 para obterne la segunda parte del totalizador.
                        
                        ComandoCaras = ComandoSurtidor.Totalizador2;
                        ArmarTramaTx();
                        //       }

                        //}

                        break;


                    case ComandoSurtidor.Totalizador2: //Se Obtiene el Total 2     
                        TimeOut = 200;
                        Reintentos = 0;
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x04, 0x00, Manguera, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };

                        ComplementoByte();

                        do
                        {
                            EnviarComando();
                            ContaTot = 1;
                            RecibirInformacion();
                            Reintentos += 1;
                        }
                        while (FalloComunicacion == true && Reintentos < 2);
                        
                        //Almacenar el Totalizador 2
                        string Total2 = (TramaRx[10].ToString("X2") + TramaRx[8].ToString("X2"));

                        Totalizador1 = Total1 + Total2;

                        ContaTot = 0;

                        ComandoCaras = ComandoSurtidor.ObtenerTotalizador;
                        AnalizarTrama(); //se almacena los totalizadores T1 y T1 en el Totalizador general "Lectura"

                        //NoEnvioComando = false;
                        break;
                        #endregion


                    case ComandoSurtidor.ObtenerPrecio:
                        #region - ObtenerPrecio
                        //NoEnvioComando = true; //Bandera utilizada para no enviar el Comando dos Veces 
                        TimeOut = 500;
                        //case ComandoSurtidor.ObtenerPrecio: $ PRECIO DE VENTA ACTUAL EN LA MANGUERA
                        AsignarMangueraClaseI();
                        AsignacionCaraClaseIII();

                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x00, 0x00, Manguera, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        ComplementoByte();
                        EnviarComando();
                        ContaTot = 1; //PARA NO ANALIZAR TRAMA
                        RecibirInformacion();

                        string PreciVentaHL = ((TramaRx[10]).ToString("X2") + (TramaRx[8]).ToString("X2"));
                        VentaPrecio = Convert.ToDecimal(Convert.ToInt32(PreciVentaHL, 16));
                        
                        //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = 0;
                        //EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = VentaPrecio / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;
                        
                        ContaTot = 0;

                        //ComandoCaras = ComandoSurtidor.ObtenerVentaDinero;
                        //ArmarTramaTx();

                        break;

                    case ComandoSurtidor.ObtenerDespacho: //VALOR DE LA VENTA 
                        TimeOut = 200;
                        AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        ComplementoByte();

                        EnviarComando();
                        ContaTot = 1; //PARA NO ANALIZAR TRAMA
                        RecibirInformacion();

                        string VentaDineroHML = ((TramaRx[10]).ToString("X2") + (TramaRx[8]).ToString("X2") + (TramaRx[6]).ToString("X2"));
                        VentaDinero = Convert.ToDecimal(VentaDineroHML);

                        ComandoCaras = ComandoSurtidor.ObtenerVentaVolumen;
                        ArmarTramaTx();
                        break;


                    case ComandoSurtidor.ObtenerVentaVolumen:
                        TimeOut = 200;
                        AsignacionCaraClaseIII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x26, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        ComplementoByte();

                        EnviarComando();
                        ContaTot = 1; //PARA NO ANALIZAR TRAMA
                        RecibirInformacion();


                        string VentaVolumenHML = ((TramaRx[10]).ToString("X2") + (TramaRx[8]).ToString("X2") + (TramaRx[6]).ToString("X2"));
                        VentaVolumen = Convert.ToDecimal(VentaVolumenHML);

                        ComandoCaras = ComandoSurtidor.ObtenerDespacho;
                        AnalizarTrama();

                        //NoEnvioComando = false;
                        ContaTot = 0;

                        break;
                        #endregion;


                    case ComandoSurtidor.Estado:
                        TimeOut = 200;
                        AsignacionCaraClaseI();
                        TramaTx = new byte[5] { 0x00, 0x00, CaraWayne, 0x00, 0xFF };
                        ComplementoByte();

                        break;

                    case ComandoSurtidor.AutorizarDespacho:
                        TimeOut = 200;
                        AsignacionCaraClaseII();
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x8F, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                        ComplementoByte();

                        break;


                    case ComandoSurtidor.Predeterminar:
                        TimeOut = 500;
                        AsignacionCaraClaseIII();
                        string strPreset = Convert.ToInt32(strValorImporte).ToString().PadLeft(6, '0');
                        byte PresetA = Convert.ToByte(strPreset.Substring(strPreset.Length - 6, 2), 16);
                        byte PresetM = Convert.ToByte(strPreset.Substring(strPreset.Length - 4, 2), 16);
                        byte PresetB = Convert.ToByte(strPreset.Substring(strPreset.Length - 2, 2), 16);
                        TramaTx = new byte[13] { 0x00, 0x00, CaraWayne, 0x00, 0x21, 0x00, PresetB, 0x00, PresetM, 0x00, PresetA, 0x00, 0xFF };
                        ComplementoByte();

                        break;

                    //case ComandoSurtidor.Totalizador3:
                    //    AsignacionCaraClaseIII();
                    //    Manguera = 00;
                    //    TramaTx = new byte[13] { 0x00, 0x00, CaraEncuestada, 0x00, 0x02, 0x00, Manguera, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                    //    break;

                    //case ComandoSurtidor.Totalizador4:
                    //    AsignacionCaraClaseIII();
                    //    Manguera = 00;
                    //    TramaTx = new byte[13] { 0x00, 0x00, CaraEncuestada, 0x00, 0x04, 0x00, Manguera, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
                    //    break;

                }

            }


            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor de el método ArmarTramaTx";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }



        //Calcula los bytes de redundancia, los 2 primeros Bytes son siempre 0x00 y 0x00
        private void ComplementoByte()
        {
            for (int i = 3; i < TramaTx.Length - 1; i += 2)
            {
                TramaTx[i] = Convert.ToByte(0xFF - TramaTx[i - 1]);
            }

        }

        private void AsignacionCaraClaseI() //Codigo de cara Encuestada para Comando ESTADO 
        {
            switch (CaraEncuestada)
            {
                case 1:
                    CaraWayne = 0x09;
                    break;
                case 2:
                    CaraWayne = 0x11;
                    break;
                case 3:
                    CaraWayne = 0x19;
                    break;
                case 4:
                    CaraWayne = 0x21;
                    break;
                case 5:
                    CaraWayne = 0x29;
                    break;
                case 6:
                    CaraWayne = 0x31;
                    break;
                case 7:
                    CaraWayne = 0x39;
                    break;
                case 8:
                    CaraWayne = 0x41;
                    break;
                case 9:
                    CaraWayne = 0x49;
                    break;
                case 10:
                    CaraWayne = 0x51;
                    break;
                case 11:
                    CaraWayne = 0x59;
                    break;
                case 12:
                    CaraWayne = 0x61;
                    break;
                case 13:
                    CaraWayne = 0x69;
                    break;
                case 14:
                    CaraWayne = 0x71;
                    break;
                case 15:
                    CaraWayne = 0x79;
                    break;
                case 16:
                    CaraWayne = 0x81;
                    break;
            }

        }

        private void AsignacionCaraClaseII()//Codigo de cara Encuestada para Comando AUTORIZAR 
        {
            switch (CaraEncuestada)
            {
                case 1:
                    CaraWayne = 0x08;
                    break;
                case 2:
                    CaraWayne = 0x10;
                    break;
                case 3:
                    CaraWayne = 0x18;
                    break;
                case 4:
                    CaraWayne = 0x20;
                    break;
                case 5:
                    CaraWayne = 0x28;
                    break;
                case 6:
                    CaraWayne = 0x30;
                    break;
                case 7:
                    CaraWayne = 0x38;
                    break;
                case 8:
                    CaraWayne = 0x40;
                    break;
                case 9:
                    CaraWayne = 0x48;
                    break;
                case 10:
                    CaraWayne = 0x50;
                    break;
                case 11:
                    CaraWayne = 0x58;
                    break;
                case 12:
                    CaraWayne = 0x60;
                    break;
                case 13:
                    CaraWayne = 0x68;
                    break;
                case 14:
                    CaraWayne = 0x70;
                    break;
                case 15:
                    CaraWayne = 0x78;
                    break;
                case 16:
                    CaraWayne = 0x80;
                    break;
            }
        }

        private void AsignacionCaraClaseIII()//Codigo de cara Encuestada para Comando PRECIO DE VENTA 
        {
            switch (CaraEncuestada)
            {
                case 1:
                    CaraWayne = 0x0F;
                    break;
                case 2:
                    CaraWayne = 0x17;
                    break;
                case 3:
                    CaraWayne = 0x1F;
                    break;
                case 4:
                    CaraWayne = 0x27;
                    break;
                case 5:
                    CaraWayne = 0x2F;
                    break;
                case 6:
                    CaraWayne = 0x37;
                    break;
                case 7:
                    CaraWayne = 0x3F;
                    break;
                case 8:
                    CaraWayne = 0x47;
                    break;
                case 9:
                    CaraWayne = 0x4F;
                    break;
                case 10:
                    CaraWayne = 0x57;
                    break;
                case 11:
                    CaraWayne = 0x5F;
                    break;
                case 12:
                    CaraWayne = 0x67;
                    break;
                case 13:
                    CaraWayne = 0x6F;
                    break;
                case 14:
                    CaraWayne = 0x77;
                    break;
                case 15:
                    CaraWayne = 0x7F;
                    break;
                case 16:
                    CaraWayne = 0x87;
                    break;
            }
        }


        private void AsignarMangueraClaseI()
        {
            switch (EstructuraRedSurtidor[CaraEncuestada].GradoCara)//) //Manguera Levantada
            {


                case 0:
                    Manguera = 0x00; //Manguera 1
                    break;

                case 1:
                    Manguera = 0x01; //Manguera 2
                    break;

                case 2:
                    Manguera = 0x02; //Manguera 3
                    break;

                case 3:
                    Manguera = 0x03; //Manguera 4
                    break;
            }
        }


        private void AsignarMangueraClaseII()
        {
            switch (EstructuraRedSurtidor[CaraEncuestada].GradoCara)//) //Manguera Levantada
            {
                    

                case 0:
                    Manguera = 0x30; //Manguera 1
                    break;

                case 1:
                    Manguera = 0x31; //Manguera 2
                    break;

                case 2:
                    Manguera = 0x32; //Manguera 3
                    break;

                case 3:
                    Manguera = 0x33; //Manguera 4
                    break;
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
                    "|" + CaraEncuestada + "|Tx|" + ComandoCaras + "|" + strTrama);

                SWTramas.Flush();
                ///////////////////////////////////////////////////////////////////////////////////

                //Almacena la cantidad de byte eco, que vendría en la trama de respuesta
                eco = Convert.ToByte(TramaTx.Length); //respuesta del LOOP de Corriente

                //Tiempo muerto mientras el Surtidor Responde
                Thread.Sleep(300);//Prueba
                //Thread.Sleep(TimeOut); //Real  
            }
            
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor de la Clase EnviarComando";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //LEE Y ALMACENA LA TRAMA RECIBIDA
        private void RecibirInformacion()
        {
            try
            {
                int Bytes = PuertoCom.BytesToRead;

                //if (!TramaEco) //Prueba de Simulador 
                    eco = 0;

                //Si la Interfase de comunicacion retorna el mensaje con ECO, se suma este a BytesEsperados
                BytesEsperados = 0x0d + eco;

                //Solo analiza los datos recibidos si la trama tiene la cantidad de Bytes Esperados
                if (Bytes >= BytesEsperados)
                {
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
                         "|" + CaraEncuestada + "|Rx|" + ComandoCaras + "|" + strTrama);

                    SWTramas.Flush();
                    ///////////////////////////////////////////////////////////////////////////////////

                    //Revisa si existe problemas en la trama
                    if (ComprobarIntegridadTrama())
                    {
                        if (ContaTot != 1)
                        {
                            AnalizarTrama();
                        }
                    }
                    else
                    {
                        FalloComunicacion = true;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Comando " + ComandoCaras + ". Bytes con daño en integridad de trama");
                        SWRegistro.Flush();
                    }
                }
                else if (FalloComunicacion == false)
                {
                    FalloComunicacion = true;
                    if (!EstructuraRedSurtidor[CaraEncuestada].FalloReportado)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|" + ComandoCaras + ". Bytes Esperados: " + BytesEsperados + " - Bytes Recibidos: " + Bytes);
                        SWRegistro.Flush();
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo RecibirInformacion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //REVISA LA INTEGRIDAD DE LA TRAMA
        private bool ComprobarIntegridadTrama()
        {
            try
            {

                if (TramaRx[12] != 0xFF) // Termidanor de Trama 
                {
                    return false;
                }

                //Todos los mensajes que provienen del surtidor vienen en tramas con Bytes pares: Byte Dato y Byte Complemento
                for (int i = 2; i < TramaRx.Length - 1; i += 2)
                {
                    if (TramaRx[i + 1] != 0xFF - TramaRx[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ComprobarIntegridadTrama";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
                return false;
            }
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
                    case ComandoSurtidor.AutorizarDespacho:
                        //case ComandoSurtidor.Inicializar:                    
                        //case ComandoSurtidor.DetenerSurtidor:
                        RecuperarEstado();

                        break;



                    //case ComandoSurtidor.ObtenerGrado:
                    //    EstructuraRedSurtidor[CaraEncuestada].GradoCara = TramaRx[0] & 0x0F;
                    //    break;

                    case ComandoSurtidor.ObtenerTotalizador:
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura =

                               Convert.ToDecimal(Totalizador1) / EstructuraRedSurtidor[CaraEncuestada].FactorTotalizador;

                        }

                        break;


                    //case ComandoSurtidor.ObtenerPrecio:

                    //    EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = VentaPrecio / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;
                    //    break;


                    case ComandoSurtidor.ObtenerDespacho:
                        //Se obtienen los valores obtenidos en la trama 
                        EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = VentaPrecio / EstructuraRedSurtidor[CaraEncuestada].FactorPrecio;
                        EstructuraRedSurtidor[CaraEncuestada].TotalVenta = VentaDinero / EstructuraRedSurtidor[CaraEncuestada].FactorImporte;
                        EstructuraRedSurtidor[CaraEncuestada].Volumen = VentaVolumen / EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;

                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo AnalizarTrama";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //ANALIZA EL ESTADO DE LA CARA Y SE LO ASIGNA A LA POSICION RESPECTIVA
        private void RecuperarEstado()
        {
            try
            {
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;


                /*Estados: 
                -	07 Mangueras colgadas
                -	00 Manguera 1 descolgada
                -	01 Manguera 2 descolgada
                -	02 Manguera 3 descolgada
                -	03 Manguera 4 descolgada
                -	0F Cara preseteada
                -	8X :
                o	X=8 manguera 1 despacho
                o	X=9 manguera 2 despacho
                o	X=A Manguera 3 despacho
                o	X=B Manguera 4 despacho
                o	X=F Termino la carga                
               */

                //Almacena en archivo el estado actual del surtidor

                byte CodigoEstado = TramaRx[10]; // 10 Bit  de estatus
                //Asigna Estado
                switch (CodigoEstado)
                {
                    case (0x07):
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior == EstadoCara.Despacho) //recuperar Ventas en caso de que no entregue el Estado 8F
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoA;

                        }
                        else
                        {
                            EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Espera;
                        }
                        break;

                    case (0x00)://Manguera 1 Descolgada
                    case (0x01)://Manguera 2 Descolgada
                    case (0x02)://Manguera 3 Descolgada
                    case (0x03)://Manguera 4 Descolgada
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.PorAutorizar;
                        //Obtener Grado de la cara
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = CodigoEstado; //Asignacion de Manguera levantada
                        EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = EstructuraRedSurtidor[CaraEncuestada].GradoCara;
                        break;
                         
                    case (0x88)://Manguera 1 Despachando
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = 0x00;
                        break;


                    case (0x89)://Manguera 2 Despachando
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = 0x01;

                        break;


                    case (0x0A)://Manguera 3 Despachando
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = 0x02;

                        break;


                    case (0x0B)://Manguera 4 Despachando
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Despacho;
                        EstructuraRedSurtidor[CaraEncuestada].GradoCara = 0x03;
                        break;


                    case (0x8F)://termino la Carga
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.FinDespachoA;
                        break;

                    case (0xF1)://Autorizado listo para la vender
                        EstructuraRedSurtidor[CaraEncuestada].Estado = EstadoCara.Autorizado;
                        break;

                    case (0xF5)://Autorizado listo para la vender

                        break;



                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Estado Indeterminado: " + CodigoEstado.ToString("X2"));
                        SWRegistro.Flush();
                        break;
                }


                //New valores actuales de venta DCF

                EstructuraRedSurtidor[CaraEncuestada].Volumen = (Convert.ToDecimal(Convert.ToInt32((TramaRx[4].ToString("X2") + TramaRx[6].ToString("X2")), 16))) /
                EstructuraRedSurtidor[CaraEncuestada].FactorVolumen;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = EstructuraRedSurtidor[CaraEncuestada].Volumen *
                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoVenta].PrecioNivel1;



                //Almacena en archivo el estado actual del surtidor
                if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Estado|" + EstructuraRedSurtidor[CaraEncuestada].Estado);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo AsignarEstado";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }

            ////Almacena en archivo el estado actual del surtidor
            //if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
            //    EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior = EstructuraRedSurtidor[CaraEncuestada].Estado;


        }

        //CONSTRUYE LOS VALORES OBTENIDOS EN LA TRAMA DE RECEPCIÓN PROVENIENTE DEL SURTIDOR
        private decimal ObtenerValor(int PosicionInicial, int PosicionFinal)
        {
            try
            {
                decimal ValorObtenido = new decimal();

                string strValorEnTrama = "";
                for (int i = PosicionFinal; i >= PosicionInicial; i -= 2)
                    strValorEnTrama += TramaRx[i].ToString("X2");

                ValorObtenido = Convert.ToDecimal(strValorEnTrama);

                return ValorObtenido;
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ObtenerValor";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
                return 0;
            }
        } ///No Utilizado!!!!!

        #endregion



        //DEPENDIENDO DEL ESTADO EN QUE SE ENCUENTRE LA CARA, SE TOMAN LAS RESPECTIVAS ACCIONES
        private void TomarAccion()
        {
            try
            {
                //Realiza la respectiva tarea en la normal ejecución del proceso
                switch (EstructuraRedSurtidor[CaraEncuestada].Estado)
                {
                    case (EstadoCara.Espera):
                        //Informa cambio de estado
                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado)
                        {
                            int mangueraColgada = EstructuraRedSurtidor[CaraEncuestada].GradoCara;//-1;/// OJO

                            oEventos.InformarCaraEnReposo(ref CaraEncuestada, ref mangueraColgada);
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Manguera " + mangueraColgada + " Informa cara en Espera");
                            SWRegistro.Flush();
                        }

                        //Revisa si las lecturas deben ser tomadas o no (Evento Apertura o Cierre de Turno)
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true ||
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)

                            LecturaAperturaCierre();

                        break;

                    case (EstadoCara.PorAutorizar):
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno mientras la cara está en Estado de Error


                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {

                            string MensajeErrorLectura = "Manguera descolgada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Finales. " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;


                        }

                        if (EstructuraRedSurtidor[CaraEncuestada].EstadoAnterior != EstructuraRedSurtidor[CaraEncuestada].Estado &&
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                        {
                            if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                            {
                                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerPrecio))
                                {
                                    EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado = EstructuraRedSurtidor[CaraEncuestada].GradoCara;
                                    int IdProducto =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto;
                                    int IdManguera =
                                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;
                                    string Lectura =
                                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura.ToString("N3");

                                    oEventos.RequerirAutorizacion(ref CaraEncuestada, ref IdProducto, ref IdManguera, ref Lectura);

                                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa requerimiento de autorizacion. Grado: "
                                        + EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado + " - Producto: " +
                                        IdProducto + " - Manguera: " + IdManguera + " - Lectura: " + Lectura);
                                    SWRegistro.Flush();
                                }
                            }

                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio comando de obtener Totalizador para Lectura Inicial Venta");
                                SWRegistro.Flush();
                            }

                        }


                        //Revisa en el vector de Autorizacion si la venta se debe autorizar
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)  /// se autorizan sin abrir turno ???
                        {
                            EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura;

                            string strLecturasVolumen =
                                EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");
                            oEventos.InformarLecturaInicialVenta(ref CaraEncuestada, ref strLecturasVolumen);

                            //Loguea Evento de envio de lectura
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informar Lectura Inicial de Venta: " +
                                strLecturasVolumen);
                            SWRegistro.Flush();

                            //Valor de Predeterminacion en $$
                            if (EstructuraRedSurtidor[CaraEncuestada].PredeterminarImporte)
                            {
                                strValorImporte = Convert.ToString(Convert.ToInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado *
                                EstructuraRedSurtidor[CaraEncuestada].FactorImporte)).PadLeft(6, '0');


                                //strValorVolumen = Convert.ToString(Convert.ToUInt32(EstructuraRedSurtidor[CaraEncuestada].ValorPredeterminado /
                                //    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].PrecioNivel1 *
                                //    EstructuraRedSurtidor[CaraEncuestada].FactorVolumen)).PadLeft(6, '0');

                                ProcesoEnvioComando(ComandoSurtidor.Predeterminar);


                            }

                            //int Reintenos = 1;
                            //do
                            //{
                            if (!ProcesoEnvioComando(ComandoSurtidor.AutorizarDespacho))
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondió comando de Autorizar Despacho");
                                SWRegistro.Flush();
                            }
                            else
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Autorización enviado con éxito");
                                SWRegistro.Flush();
                            }
                            //Reintenos++;
                            //} while ((EstructuraRedSurtidor[CaraEncuestada].Estado != EstadoCara.PorAutorizar) && Reintenos <= 2);// ||
                            //EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimReposo) &&
                            //Reintenos <= 2);


                            //Reset del elemento que indica que la Cara debe ser autorizada y setea elemento que indica que la venta inicio
                            if (EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Autorizado ||
                                EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.Despacho)// ||
                            //EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoD0 ||
                            //EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoD4 ||
                            //EstructuraRedSurtidor[CaraEncuestada].Estado == EstadoCara.TokheimDespachoF0)
                            {
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Comando Autorización Aceptado");
                                SWRegistro.Flush();
                                EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;
                                EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                            }
                        }




                        break;

                    case EstadoCara.Autorizado:
                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara Autorizada";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                        break;

                    case EstadoCara.FinDespachoA:

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma deLecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma deLecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;

                        //Revisa si es necesario detener la venta en curso por Monitoreo de Chip
                        //if (EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara)
                        //{
                        //    if (!ProcesoEnvioComando(ComandoSurtidor.DetenerSurtidor))
                        //    {
                        //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No aceptó comando de detención de venta");
                        //        SWRegistro.Flush();
                        //    }
                        //    else
                        //    {
                        //        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Venta detenida");
                        //        SWRegistro.Flush();
                        //        EstructuraRedSurtidor[CaraEncuestada].DetenerVentaCara = false;
                        //    }
                        //}

                        //Se obtienen los valores de parciales de despacho
                        //if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDespacho))
                        //{

                        //    //Reporta los valores de parciales de despacho                
                        //    string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N1");
                        //    string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                        //    oEventos.InformarVentaParcial(ref CaraEncuestada, ref strTotalVenta, ref strVolumen);
                        //}

                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                            ProcesoFindeVenta();

                        break;

                    case EstadoCara.Despacho:

                        //Envía ERROR EN TOMA DE LECTURAS, si se requiere cerrar o abrir turno durante el despacho
                        if (EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno == false)
                        {
                            string MensajeErrorLectura = "Cara en Fin de Despacho";
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                            {
                                bool EstadoTurno = false;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Iniciales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                            {
                                bool EstadoTurno = true;
                                EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                                oEventos.ReportarCancelacionTurno(ref CaraEncuestada, ref MensajeErrorLectura, ref EstadoTurno);
                                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Fallo en toma de Lecturas Finales: " + MensajeErrorLectura);
                                SWRegistro.Flush();
                            }
                            //Se establece valor de la variable para que indique que ya fue reportado el error
                            EstructuraRedSurtidor[CaraEncuestada].FalloTomaLecturaTurno = true;
                        }

                        //Reset del elemento que indica que la Cara debe ser autorizada
                        if (EstructuraRedSurtidor[CaraEncuestada].AutorizarCara == true)
                            EstructuraRedSurtidor[CaraEncuestada].AutorizarCara = false;

                        //Setea elemento que indica que se inicia una venta y TIENE que finalizarse
                        if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial == false)
                            EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = true;
                        break;

                    //case EstadoCara.TokheimFinDespacho:
                    //case EstadoCara.FinDespachoForzado:
                    //    if (EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial)
                    //        ProcesoFindeVenta();
                    //    break;

                    default:
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Estado Indeterminado: " + EstructuraRedSurtidor[CaraEncuestada].Estado);
                        SWRegistro.Flush();
                        break;
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo TomarAccion";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        //REALIZA PROCESO DE FIN DE VENTAF
        private void ProcesoFindeVenta()
        {
            try
            {
                //Inicializacion de variables
                EstructuraRedSurtidor[CaraEncuestada].Volumen = 0;
                EstructuraRedSurtidor[CaraEncuestada].TotalVenta = 0;
                EstructuraRedSurtidor[CaraEncuestada].PrecioVenta = 0;

                if (!ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador))
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No acepto comando de obtencion de totalizadores para Lectura Final de Venta");
                    SWRegistro.Flush();
                }
                else
                    EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta =
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura;

                if (ProcesoEnvioComando(ComandoSurtidor.ObtenerDespacho))
                {
                    //Evalúa si la venta viene en 0
                    if (EstructuraRedSurtidor[CaraEncuestada].Volumen != 0 || EstructuraRedSurtidor[CaraEncuestada].TotalVenta != 0)
                    {
                        //Almacena los valores en las variables requerida por el Evento


                        string strTotalVenta = EstructuraRedSurtidor[CaraEncuestada].TotalVenta.ToString("N3");
                        string strPrecio = EstructuraRedSurtidor[CaraEncuestada].PrecioVenta.ToString("N3");
                        string strLecturaFinalVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaFinalVenta.ToString("N3");
                        string strVolumen = EstructuraRedSurtidor[CaraEncuestada].Volumen.ToString("N3");
                        string strLecturaInicialVenta = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].LecturaInicialVenta.ToString("N3");
                        byte bytProducto = Convert.ToByte(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].IdProducto);
                        int IdManguera = EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].MangueraBD;

                        //Si pudo finalizar correctamente el proceso de toma de datos de fin de venta, sete bandera indicadora de Venta Finalizada
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;

                        //Loguea evento Fin de Venta
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|InformarFinalizacionVenta. Importe: " + strTotalVenta +
                            " - Precio: " + strPrecio + " - Lectura Inicial: " + strLecturaInicialVenta + " - Lectura Final: " + strLecturaFinalVenta +
                            " - Volumen: " + strVolumen + " - Producto: " + bytProducto + " - Manguera: " + IdManguera);
                        SWRegistro.Flush();

                        String PresionLLenado = "0";
                        //oEventos.InformarFinalizacionVenta(ref CaraEncuestada, ref strTotalVenta, ref strPrecio, ref strLecturaFinalVenta,
                        //          ref strVolumen, ref bytProducto, ref IdManguera, ref PresionLLenado, ref strLecturaInicialVenta);
                        oEventos.InformarFinalizacionVenta(ref CaraEncuestada, ref strTotalVenta, ref strPrecio, ref strLecturaFinalVenta,
                                  ref strVolumen, ref bytProducto, ref IdManguera, ref PresionLLenado, ref strLecturaInicialVenta);
                    }
                    else
                    {
                        oEventos.ReportarVentaEnCero(ref CaraEncuestada);
                        EstructuraRedSurtidor[CaraEncuestada].EsVentaParcial = false;
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "Proceso|Venta en CERO");
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No acepto comando de obtencion de datos de Final de Venta");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Constructor del metodo ProcesoFindeVenta";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }

        private void LecturaAperturaCierre()
        {
            try
            {
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia Toma de Lectura para Apertura/Cierre de Turno");
                SWRegistro.Flush();


                //int Num_manguerasXcara = 2;
                int NMangueras = 0;


                foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados) //recorro las caras 
                {
                    //int MangueraBDD = (Convert.ToInt32(Grado.NoGrado)) + 1;

                    //Mangueras por cara solo son 2 Utilizada para 
                    EstructuraRedSurtidor[CaraEncuestada].GradoCara = Grado.NoGrado; //MangueraEncuestada;
                  

                    MangueraEncuestada += 1;


                    if (ProcesoEnvioComando(ComandoSurtidor.ObtenerTotalizador)) ///REA LIZAR 2 VECE E INCREMENTAR LA MANGUERA
                    {
                        //EstructuraRedSurtidor[CaraEncuestada].GradoCara = MangueraEncuestada; //Asignacion de la manguera a obtener Totalizador; 0=M1; 1=M2
                        
                        if (MangueraEncuestada == 2)//cantidad de manguera por cara 
                        {
                            MangueraEncuestada = 0;
                        }

                        //Cambia el precio si es apertura de turno
                        if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                        {
                            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Inicia cambio de precios");
                            SWRegistro.Flush();
                            CambiarPrecios();
                        }


                    }


                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio comando de obtener Totalizador para Lectura Inicial/Final de Turno");
                        SWRegistro.Flush();
                    }
                    //SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Arma Lecturas para turno. Manguera " +
                    //          MangueraBDD + " - Lectura " +
                    //          EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura);
                    //SWRegistro.Flush();

                    System.Collections.ArrayList ArrayLecturas = new System.Collections.ArrayList();


                    //foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                    //{
                    ArrayLecturas.Add(Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD) + "|" +
                        Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura) + "|" +
                        Convert.ToString(EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioSurtidorNivel1));

                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Arma Lecturas para turno. Manguera " +
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].MangueraBD + " - Lectura " +
                         EstructuraRedSurtidor[CaraEncuestada].ListaGrados[EstructuraRedSurtidor[CaraEncuestada].GradoAutorizado].Lectura);
                    SWRegistro.Flush();
                    //}



                    System.Array LecturasEnvio = System.Array.CreateInstance(typeof(string), ArrayLecturas.Count);
                    ArrayLecturas.CopyTo(LecturasEnvio);


                    //Lanza evento, si las lecturas pedidas son para CIERRE DE TURNO
                    if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno == true)
                    {
                        oEventos.InformarLecturaFinalTurno(ref LecturasEnvio);
                        
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lecturas Finales de turno");
                        SWRegistro.Flush();
                        if (NMangueras == EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1)
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaCierreTurno = false;
                    }
                    //Lanza evento, si las lecturas pedidas son para APERTURA DE TURNO
                    if (EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno == true)
                    {
                        oEventos.InformarLecturaInicialTurno(ref LecturasEnvio);
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Informa Lecturas Iniciales de turno");
                        SWRegistro.Flush();
                        if (NMangueras == EstructuraRedSurtidor[CaraEncuestada].ListaGrados.Count - 1)
                            EstructuraRedSurtidor[CaraEncuestada].TomarLecturaAperturaTurno = false;
                    }



                    NMangueras += 1;
                }
            }


            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo LecturaAperturaCierre: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }


        private void CambiarPrecios()
        {
            try
            {
                if (ProcesoEnvioComando(ComandoSurtidor.EstablecerPrecio))
                {
                    if (TramaRx[4] == 0x01)
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Precios aceptados por Surtidor");
                        SWRegistro.Flush();
                    }
                    else
                    {
                        SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|Cambio de precio rechazado por Surtidor");
                        SWRegistro.Flush();
                    }
                }
                else
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Error|No respondio comando Establecer Precio");
                    SWRegistro.Flush();
                }
                foreach (Grados Grado in EstructuraRedSurtidor[CaraEncuestada].ListaGrados)
                {
                    SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Proceso|Grado: " + Grado.NoGrado + " - Precio: " +
                        EstructuraRedSurtidor[CaraEncuestada].ListaGrados[Grado.NoGrado].PrecioNivel1);
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Metodo CambiarPrecios: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }

        #endregion


        #region EVENTOS DE LA CLASE

        private void oEvento_VentaAutorizada(ref byte Cara, ref string Precio, ref string ValorProgramado, ref byte TipoProgramacion, ref string Placa, ref int MangueraProgramada, ref bool EsVentaGerenciada)
        {


            try
            {
                if (EstructuraRedSurtidor.ContainsKey(Cara))
                {
                    //Loguea evento                
                    SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Evento|Recibe Autorizacion. Valor Programado " + ValorProgramado +
                                            " - Tipo de Programacion: " + TipoProgramacion + " - Manguera: " + MangueraProgramada +
                                            " - Gerenciada: " + EsVentaGerenciada);
                    SWRegistro.Flush();

                    //Bandera que indica que la cara debe autorizarse para despachar
                    EstructuraRedSurtidor[Cara].AutorizarCara = true; //se activa sin abrir turno ???

                    //Valor a programar
                    EstructuraRedSurtidor[Cara].ValorPredeterminado = Convert.ToDecimal(ValorProgramado);

                    EstructuraRedSurtidor[Cara].PrecioVenta = Convert.ToDecimal(Precio);

                    EstructuraRedSurtidor[Cara].EsVentaGerenciada = EsVentaGerenciada;

                    //Si viene valor para predeterminar setea banderas
                    if (EstructuraRedSurtidor[Cara].ValorPredeterminado != 0)
                    {
                        //1 predetermina Volumen, 0 predetermina Dinero
                        if (TipoProgramacion == 1)
                        {
                            EstructuraRedSurtidor[Cara].PredeterminarImporte = false;
                            EstructuraRedSurtidor[Cara].PredeterminarVolumen = true;
                        }
                        else
                        {
                            EstructuraRedSurtidor[Cara].PredeterminarImporte = true;
                            EstructuraRedSurtidor[Cara].PredeterminarVolumen = false;
                        }
                    }
                    else
                    {
                        EstructuraRedSurtidor[Cara].PredeterminarImporte = false;
                        EstructuraRedSurtidor[Cara].PredeterminarVolumen = false;
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Constructor del método oEvento_VentaAutorizada";
                SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Excepcion|" + MensajeExcepcion + ": " + Excepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_TurnoAbierto(ref string Surtidores, ref string PuertoTerminal, ref System.Array Precios)
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
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraLectura].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;
                            }

                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaAperturaTurno = true;
                            }

                            //Guarda los precios del Producto de cada grado de la cara
                            for (int ContadorGrados = 0; ContadorGrados <= EstructuraRedSurtidor[CaraLectura].ListaGrados.Count - 1; ContadorGrados++)
                            {
                                EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel1 =
                                    Grados[EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel1;
                                EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].PrecioNivel2 =
                                    Grados[EstructuraRedSurtidor[CaraLectura].ListaGrados[ContadorGrados].MangueraBD].PrecioNivel2;

                            }

                        }
                    }
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoAbierto: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEvento_TurnoCerrado(ref string Surtidores, ref string PuertoTerminal)
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
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaCierreTurno = true;

                            }
                        }

                        //Organiza banderas de pedido de lecturas para la cara PAR
                        CaraLectura = Convert.ToByte(Convert.ToInt16(bSurtidores[i]) * 2);

                        //Evalúa si la Cara a tomar las lecturas, pertenece a esta red de surtidores
                        if (EstructuraRedSurtidor.ContainsKey(CaraLectura))
                        {
                            //Setea la variable de impresión de Fallo de toma lectura
                            EstructuraRedSurtidor[CaraLectura].FalloTomaLecturaTurno = false;

                            //Si la cara esta activa se solicita la toma de lecturas en la apertura
                            if (EstructuraRedSurtidor[CaraLectura].Activa)
                            {
                                //Activa bandera que indica que deben tomarse las Lecturas Iniciales
                                EstructuraRedSurtidor[CaraLectura].TomarLecturaCierreTurno = true;
                            }
                        }
                    }
                }

            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepcion en el Evento oEvento_TurnoCerrado: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|Surtidores|" + Surtidores + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }
        }
        private void oEventos_FinalizarVentaPorMonitoreoCHIP(ref byte Cara)
        {
            try
            {
                if (EstructuraRedSurtidor.ContainsKey(Cara))
                {
                    EstructuraRedSurtidor[Cara].DetenerVentaCara = true;
                    SWRegistro.WriteLine(DateTime.Now + "|" + Convert.ToString(Cara) + "|Evento|Recibe Detencion por Monitoreo de Chip");
                    SWRegistro.Flush();
                }
            }
            catch (Exception Excepcion)
            {
                string MensajeExcepcion = "Excepción en el Evento oEventos_FinalizarVentaPorMonitoreoCHIP: " + Excepcion;
                SWRegistro.WriteLine(DateTime.Now + "|" + Cara + "|Excepcion|" + MensajeExcepcion);
                SWRegistro.Flush();
            }

        }
        //private void oEventos_ProgramarCambioPrecioKardex(ref ColMangueras mangueras)
        //{
        //    //Evento que manda a cambiar el producto y su respectivo precio en las mangueras
        //    try
        //    {
        //        //Recorriendo la coleccion de mangueras para saber a cuales les debo cambiar el producto y el precio
        //        foreach (Manguera OManguera in mangueras)
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
        //                        SWRegistro.WriteLine(DateTime.Now + "|" + ORedSurtidor.CaraBD + 
        //                            "|Evento| Recibe evento para Cambio Precio Kardex. Manguera: " + OGrado.MangueraBD +
        //                            " - Producto: " + OGrado.IdProducto + " - Solicitud de cambio de producto");
        //                        SWRegistro.Flush();
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception Excepcion)
        //    {
        //        string MensajeExcepcion = "Excepcion en el Evento oEventos_ProgramarCambioPrecioKardex: " + Excepcion;
        //        SWRegistro.WriteLine(DateTime.Now + "|Excepcion|" + MensajeExcepcion);
        //        SWRegistro.Flush();
        //    }
        //}
        private void oEventos_CerrarProtocolo()
        {
            SWRegistro.WriteLine(DateTime.Now + "|" + CaraEncuestada + "|Evento|Recibe evento de detencion de Protocolo");
            SWRegistro.Flush();
            this.CondicionCiclo = false;
        }

        #endregion
    }
}