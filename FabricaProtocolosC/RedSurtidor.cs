using System;
using System.Collections.Generic;
using System.Text;

namespace POSstation.Protocolos
{
    public class RedSurtidor
    {
        //Declaración de propiedades cuyo valor es dinámico en Tiempo de Ejecución
        #region PROPIEDADES DE VALOR CAMBIANTE

        private EstadoCara _Estado = EstadoCara.Indeterminado;
        public EstadoCara Estado
        {
            get { return _Estado; }
            set { _Estado = value; }
        }

        private EstadoCara _EstadoAnterior = EstadoCara.Indeterminado;
        public EstadoCara EstadoAnterior
        {
            get { return _EstadoAnterior; }
            set { _EstadoAnterior = value; }
        }





        // para Compac -- DCF 

        private EstadoManguera _EstadoMang = EstadoManguera.Indeterminado;
        public EstadoManguera EstadoMang
        {
            get { return _EstadoMang; }
            set { _EstadoMang = value; }
        }

        private EstadoManguera _IdleAnterior = EstadoManguera.Indeterminado; //DCF para compac
        public EstadoManguera IdleAnterior
        {
            get { return _IdleAnterior; }
            set { _IdleAnterior = value; }
        }

        private EstadoManguera _IdleActual = EstadoManguera.Indeterminado; //DCF para compac
        public EstadoManguera IdleActual
        {
            get { return _IdleActual; }
            set { _IdleActual = value; }
        }


        /*- Fecha de Inclusión: 2008/03/24 13:00 -*/
        //Propiedades utilizadas para control de surtidores DEVELCO
        private HardwareStatus _EstadoHardware = HardwareStatus.Reposo;
        public HardwareStatus EstadoHardware
        {
            get { return _EstadoHardware; }
            set { _EstadoHardware = value; }
        }

        private WorkStatus _EstadoDespacho = WorkStatus.NoEnCarga;
        public WorkStatus EstadoDespacho
        {
            get { return _EstadoDespacho; }
            set { _EstadoDespacho = value; }
        }

        private StatusPulsador _Pulsador = StatusPulsador.Desactivado;
        public StatusPulsador Pulsador
        {
            get { return _Pulsador; }
            set { _Pulsador = value; }
        }

        private byte _ErrorCara;
        public byte ErrorCara
        {
            get { return _ErrorCara; }
            set { _ErrorCara = value; }
        }
        /*----------------------------------------*/

        bool _PredeterminarImporte;
        public bool PredeterminarImporte
        {
            get { return _PredeterminarImporte; }
            set { _PredeterminarImporte = value; }
        }

        bool _PredeterminarVolumen;
        public bool PredeterminarVolumen
        {
            get { return _PredeterminarVolumen; }
            set { _PredeterminarVolumen = value; }
        }

        private int _GradoCara;
        public int GradoCara
        {
            get { return _GradoCara; }
            set { _GradoCara = value; }
        }

        private int _GradoAutorizado;
        public int GradoAutorizado
        {
            get { return _GradoAutorizado; }
            set { _GradoAutorizado = value; }
        }

        private int _GradoVenta;
        public int GradoVenta
        {
            get { return _GradoVenta; }
            set { _GradoVenta = value; }
        }



        private int _GradoVentaInicial; // DCF para comparar el grado autorizado y el grado que realizo la venta  12-03-11
        public int GradoVentaInicial
        {
            get { return _GradoVentaInicial; }
            set { _GradoVentaInicial = value; }
        }

        // Identificación del protocolo a utilizar  DCF_Extended 04/05/2012
        //Gilbarco Normal = 0
        //Gilbarco Extended = 1
        private bool _Gilbarco_Extended;
        public bool Gilbarco_Extended
        {
            get { return _Gilbarco_Extended; }
            set { _Gilbarco_Extended = value; }
        }



        decimal _Volumen;                 //Almacena VOLUMEN PARCIAL y VOLUMEN FINAL de la venta
        public decimal Volumen
        {
            get { return _Volumen; }
            set { _Volumen = value; }
        }

        decimal _TotalVenta;
        public decimal TotalVenta
        {
            get { return _TotalVenta; }
            set { _TotalVenta = value; }
        }

        decimal _PrecioVenta;
        public decimal PrecioVenta
        {
            get { return _PrecioVenta; }
            set { _PrecioVenta = value; }
        }

        decimal _LecturaInicialVenta;            //Almacena la LECTURA INICIAL de cada venta en curso
        public decimal LecturaInicialVenta
        {
            get { return _LecturaInicialVenta; }
            set { _LecturaInicialVenta = value; }
        }

        decimal _LecturaFinalVenta;              //Almacena la LECTURA FINAL de cada venta en curso
        public decimal LecturaFinalVenta
        {
            get { return _LecturaFinalVenta; }
            set { _LecturaFinalVenta = value; }
        }

        decimal _Lectura;
        public decimal Lectura
        {
            get { return _Lectura; }
            set { _Lectura = value; }
        }

        bool _TomarLecturaAperturaTurno;
        public bool TomarLecturaAperturaTurno
        {
            get { return _TomarLecturaAperturaTurno; }
            set { _TomarLecturaAperturaTurno = value; }
        }

        bool _TomarLecturaCierreTurno;
        public bool TomarLecturaCierreTurno
        {
            get { return _TomarLecturaCierreTurno; }
            set { _TomarLecturaCierreTurno = value; }
        }

        /*- Agregado para Develco el 31/01/2008 ----------*/
        bool _TomarLectura;
        public bool TomarLectura
        {
            get { return _TomarLectura; }
            set { _TomarLectura = value; }
        }

        // Agreagado paraa toma de lectura por surtidor  DCF - JD 11/12/2017
        bool _TomarLectura_Surtidor;
        public bool TomarLectura_Surtidor
        {
            get { return _TomarLectura_Surtidor; }
            set { _TomarLectura_Surtidor = value; }
        }


        bool _DesautorizarDespacho;
        public bool DesautorizarDespacho
        {
            get { return _DesautorizarDespacho; }
            set { _DesautorizarDespacho = value; }
        }

        decimal _PrecioCara;
        public decimal PrecioCara
        {
            get { return _PrecioCara; }
            set { _PrecioCara = value; }
        }

        bool _FinalizarVenta;
        public bool FinalizarVenta
        {
            get { return _FinalizarVenta; }
            set { _FinalizarVenta = value; }
        }

        private byte _CodigoError;
        public byte CodigoError
        {
            get { return _CodigoError; }
            set { _CodigoError = value; }
        }

        private decimal _PresionLlenado;
        public decimal PresionLlenado
        {
            get { return _PresionLlenado; }
            set { _PresionLlenado = value; }
        }

        bool _Despacho; //Para controlar que la venta si se realizó. DCf -- 17/02/2012
        public bool Despacho
        {
            get { return _Despacho; }
            set { _Despacho = value; }
        }


        /*------------------------------------------------*/
        /*------------------------------------------------*/


        bool _ComandoFX_A5_B5;
        public bool ComandoFX_A5_B5
        {
            get { return _ComandoFX_A5_B5; }
            set { _ComandoFX_A5_B5 = value; }
        }


        bool _ComandoCX_DX_A1;
        public bool ComandoCX_DX_A1
        {
            get { return _ComandoCX_DX_A1; }
            set { _ComandoCX_DX_A1 = value; }
        }



        bool _ComandoCX_DX_A3;
        public bool ComandoCX_DX_A3
        {
            get { return _ComandoCX_DX_A3; }
            set { _ComandoCX_DX_A3 = value; }
        }


        bool _Comando262A_A9;
        public bool Comando262A_A9
        {
            get { return _Comando262A_A9; }
            set { _Comando262A_A9 = value; }
        }


        SeriesTokeim _Serie;
        public SeriesTokeim Serie
        {
            get { return _Serie; }
            set { _Serie = value; }
        }

        /*------------------------------------------------*/
        /*------------------------------------------------*/
        bool _FalloTomaLecturaTurno;
        public bool FalloTomaLecturaTurno
        {
            get { return _FalloTomaLecturaTurno; }
            set { _FalloTomaLecturaTurno = value; }
        }

        bool _FalloReportado;
        public bool FalloReportado
        {
            get { return _FalloReportado; }
            set { _FalloReportado = value; }
        }


        bool _FalloTomaLecturas;
        public bool FalloTomaLecturas
        {
            get { return _FalloTomaLecturas; }
            set { _FalloTomaLecturas = value; }
        }




        decimal _ValorPredeterminado;
        public decimal ValorPredeterminado
        {
            get { return _ValorPredeterminado; }
            set { _ValorPredeterminado = value; }
        }

        bool _CaraInicializada;
        public bool CaraInicializada
        {
            get { return _CaraInicializada; }
            set { _CaraInicializada = value; }
        }

        bool _AutorizarCara;
        public bool AutorizarCara
        {
            get { return _AutorizarCara; }
            set { _AutorizarCara = value; }
        }

        private decimal _Densidad;
        public decimal Densidad
        {
            get { return _Densidad; }
            set { _Densidad = value; }
        }

        //Manguera predeterminada para vender
        private Int16 _MangueraProgramada;
        public Int16 MangueraProgramada
        {
            get
            {
                return _MangueraProgramada;
            }
            set
            {
                _MangueraProgramada = value;
            }
        }

        //Variable bandera que indica si se inactiva la cara
        private bool _InactivarCara;
        public bool InactivarCara
        {
            get
            {
                return _InactivarCara;
            }
            set
            {
                _InactivarCara = value;
            }
        }

        //Variable bandera que indica si se activa la cara
        private bool _ActivarCara;
        public bool ActivarCara
        {
            get
            {
                return _ActivarCara;
            }
            set
            {
                _ActivarCara = value;
            }
        }

        private string _PuertoParaImprimir;
        public string PuertoParaImprimir
        {
            get
            {
                return _PuertoParaImprimir;
            }
            set
            {
                _PuertoParaImprimir = value;
            }
        }

        private bool _FueComandoAceptado;
        public bool FueComandoAceptado
        {
            get { return _FueComandoAceptado; }
            set { _FueComandoAceptado = value; }
        }

        private bool _CambiarDensidad;
        public bool CambiarDensidad
        {
            get { return _CambiarDensidad; }
            set { _CambiarDensidad = value; }
        }

        //Variable bandera que indica si se va a cambiar el producto en alguna manguera de la cara
        private bool _CambiarProductoAMangueras;
        public bool CambiarProductoAMangueras
        {
            get
            {
                return _CambiarProductoAMangueras;
            }
            set
            {
                _CambiarProductoAMangueras = value;
            }
        }

        private bool _DetenerVentaCara;
        public bool DetenerVentaCara
        {
            get { return _DetenerVentaCara; }
            set { _DetenerVentaCara = value; }
        }

        private bool _EsVentaGerenciada;
        public bool EsVentaGerenciada
        {
            get { return _EsVentaGerenciada; }
            set { _EsVentaGerenciada = value; }
        }

        #endregion

        //Declaración de propiedades cuyo valor es definido al momento de instanciar
        #region PROPIEDADES DEFINIDAS
        private bool _ObtenerDatos_Ventas; //13-06-2013 para poder pedir datos de la venta terminada en los 2A
        public bool ObtenerDatos_Ventas
        {
            get { return _ObtenerDatos_Ventas; }
            set { _ObtenerDatos_Ventas = value; }
        }
        private bool _RecuperarVenta;
        public bool RecuperarVenta
        {
            get { return _RecuperarVenta; }
            set { _RecuperarVenta = value; }
        }
        private int _conta_factorImporte; //Importer para dulex para sacar calculo de venta por totalizador de importes; dos decimales  "/100"
        public int conta_factorImporte
        {
            get { return _conta_factorImporte; }
            set { _conta_factorImporte = value; }
        }
        public bool PeticionAutorizacion { get; set; }

        private byte _Cara;
        public byte Cara
        {
            get { return _Cara; }
            set { _Cara = value; }
        }

        private byte _CaraBD;
        public byte CaraBD
        {
            get { return _CaraBD; }
            set { _CaraBD = value; }
        }

        private Int32 _IdSurtidor;
        public Int32 IdSurtidor
        {
            get { return _IdSurtidor; }
            set { _IdSurtidor = value; }
        }

        private bool _EsVentaParcial;
        public bool EsVentaParcial
        {
            get { return _EsVentaParcial; }
            set { _EsVentaParcial = value; }
        }

        private int _FactorVolumen;
        public int FactorVolumen
        {
            get { return _FactorVolumen; }
            set { _FactorVolumen = value; }
        }

        private int _FactorPrecio;
        public int FactorPrecio
        {
            get { return _FactorPrecio; }
            set { _FactorPrecio = value; }
        }

        private int _FactorTotalizador;
        public int FactorTotalizador
        {
            get { return _FactorTotalizador; }
            set { _FactorTotalizador = value; }
        }


        private int _FactorTotalizadorImporte = 100; //Importer para dulex para sacar calculo de venta por totalizador de importes; dos decimales  "/100"
        public int FactorTotalizadorImporte
        {
            get { return _FactorTotalizadorImporte; }
            set { _FactorTotalizadorImporte = value; }
        }

        private int _FactorImporte;
        public int FactorImporte
        {
            get { return _FactorImporte; }
            set { _FactorImporte = value; }
        }


        //Factor Multiplicativo precio de 5 digitos //DCF 2010-10-27 EDS el Muñeco
        // = 10 para terpel por defecto 2011.03.15-1705

        private decimal _MultiplicadorPrecioVenta = 1; //2011.03.15-1705 //10 para terpel para que funcione los precio mayores a 9999, para el resto de las estaciones =1; ojo 
        public decimal MultiplicadorPrecioVenta
        {
            get { return _MultiplicadorPrecioVenta; }
            set { _MultiplicadorPrecioVenta = value; }

        }

        //Pra Gilbarco Kraus utiliza factores de predeterminacion
        private int _FactorPredeterminacionVolumen;
        public int FactorPredeterminacionVolumen
        {
            get { return _FactorPredeterminacionVolumen; }
            set { _FactorPredeterminacionVolumen = value; }
        }

        private int _FactorPredeterminacionImporte;
        public int FactorPredeterminacionImporte
        {
            get { return _FactorPredeterminacionImporte; }
            set { _FactorPredeterminacionImporte = value; }
        }


        // contador de errores al predeterminar en wayne_Duplex 2011.03.16-1008 DCF
        private int _ContadorError;
        public int ContadorError
        {
            get { return _ContadorError; }
            set { _ContadorError = value; }
        }

        // Bandera para controlar el fin de venta  wayne_Duplex 2011.03.16-1521 DCF
        private bool _PosibleErrorFinVenta;
        public bool PosibleErrorFinVenta
        {
            get { return _PosibleErrorFinVenta; }
            set { _PosibleErrorFinVenta = value; }
        }


        private bool _Activa;
        public bool Activa
        {
            get { return _Activa; }
            set { _Activa = value; }
        }

        private decimal _VersionFirmware;
        public decimal VersionFirmware
        {
            get { return _VersionFirmware; }
            set { _VersionFirmware = value; }
        }

        private List<Grados> _ListaGrados = new List<Grados>();
        public List<Grados> ListaGrados
        {
            get { return _ListaGrados; }
            set { _ListaGrados = value; }
        }


        private Boolean _AplicaControlPresionLLenado; //08/06/2012 dcf 
        public Boolean AplicaControlPresionLLenado //08/06/2012
        {
            get
            {
                return _AplicaControlPresionLLenado;
            }
            set
            {
                _AplicaControlPresionLLenado = value;
            }
        }


        private Boolean _AplicaCambioPrecioCliente; // cambio de precio para un cliente X
        public Boolean AplicaCambioPrecioCliente
        {
            get
            {
                return _AplicaCambioPrecioCliente;
            }
            set
            {
                _AplicaCambioPrecioCliente = value;
            }
        }




        //**************************** **************************** ****************************
        //**************************** **************************** ****************************
        // Creadas para 2A por multiples estados en un solo RX

        bool _VentaTermina; // Para el control de Envio Reset en 2A
        public bool VentaTermina
        {
            get { return _VentaTermina; }
            set { _VentaTermina = value; }
        }

        bool _DespachoA2; // DespachoA2
        public bool DespachoA2
        {
            get { return _DespachoA2; }
            set { _DespachoA2 = value; }
        }


        bool _Manguera_ON; // Para el control de Envio Reset en 2A
        public bool Manguera_ON
        {
            get { return _Manguera_ON; }
            set { _Manguera_ON = value; }
        }

        bool _Enviar_ACK; // Para el control de Envio del ACK en 2A
        public bool Enviar_ACK
        {
            get { return _Enviar_ACK; }
            set { _Enviar_ACK = value; }
        }


        bool _CRC_FA; // Para el control de Envio del ACK en 2A
        public bool CRC_FA
        {
            get { return _CRC_FA; }
            set { _CRC_FA = value; }
        }

        //Egzample 1 ; CRC=1E FA  sended byte will be 1E 10 FA  
        //Egzample 2 ; CRC= FA 78 sended byte will be 10 FA 78  //16/04/2012
        bool _FA;
        public bool FA
        {
            get { return _FA; }
            set { _FA = value; }
        }

        bool _FinDespachoA2; // FinDespachoA2
        public bool FinDespachoA2
        {
            get { return _FinDespachoA2; }
            set { _FinDespachoA2 = value; }
        }


        bool _Autorizado_A2; // A2Autorizado estado
        public bool Autorizado_A2
        {
            get { return _Autorizado_A2; }
            set { _Autorizado_A2 = value; }
        }

        bool _ReposoA2; // ReposoA2 estado
        public bool ReposoA2
        {
            get { return _ReposoA2; }
            set { _ReposoA2 = value; }
        }


        bool _Bloqueado; // Bloqueo para Coritec2 //13-09-2011
        public bool Bloqueado
        {
            get { return _Bloqueado; }
            set { _Bloqueado = value; }
        }

        bool _Desbloqueado; // Bloqueo para Coritec2 //13-09-2011
        public bool Desbloqueado
        {
            get { return _Desbloqueado; }
            set { _Desbloqueado = value; }
        }


        private bool _Autorizandol;
        public bool Autorizando
        {
            get { return _Autorizandol; }
            set { _Autorizandol = value; }
        }





        //**************************** **************************** ****************************
        //**************************** **************************** ****************************
        //**************************** **************************** ****************************


        /* bool _VersionFirmware; // Para el control de Envio Reset en 2A
         public bool VersionFirmware
         {
             get { return _VersionFirmware; }
             set { _VersionFirmware = value; }
         }
         */




        #endregion

        public string GradoMangueraVentaParcial { get; set; }

        public string Guid { get; set; }

        public int Status0 { get; set; }

        public int Status1 { get; set; }

        public int Status2 { get; set; }


        decimal _SetPresionLLenado;
        public decimal SetPresionLLenado
        {
            get { return _SetPresionLLenado; }
            set { _SetPresionLLenado = value; }
        }


     //**********************************************************
     //**********************************************************

       // parametros para el Monitoreo de CHIP Detencion DCF 06/02/2018 Perú

        private int _TimeMonitoreo;
        public int TimeMonitoreo
        {
            get { return _TimeMonitoreo; }
            set { _TimeMonitoreo = value; }
        }

        bool _MonitoreoChip; 
        public bool MonitoreoChip
        {
            get { return _MonitoreoChip; }
            set { _MonitoreoChip = value; }
        }

    //**********************************************************
    //**********************************************************



    }


    //Define los posibles ESTADOS de cada una de las Caras del Surtidor
    public enum EstadoCara
    {
        #region Estados originales de Gilbarco
        Error,
        Espera,
        PorAutorizar,
        Autorizado,
        Despacho,
        FinDespachoA,
        FinDespachoB,
        Detenido,
        EsperandoDatos,
        #endregion


        #region Estados agregados para Develco (01/09/2010)
        DevelcoEspera,
        DevelcoDespacho,
        DevelcoFinDespachoHardware,
        DevelcoFinDespachoStatus,
        DevelcoPorAutorizar,
        DevelcoAutorizada,
        DevelcoIndeterminado,
        #endregion

        #region Estados agregados para PumpControl (13/11/2009)
        PumpControlEspera,
        PumpControlDespacho,
        PumpControlFinDespacho,
        PumpControlProgramacion,
        PumpControlError,
        PumpControlPorAutorizar1,
        PumpControlPorAutorizar2,
        PumpControlIniciaDespacho,
        PumpControlPredeterminado,
        PumpControlEsperandoFinFlujo,
        #endregion

        #region Estados agregados para Tokheim (13/11/209)
        TokheimNoInicializado,  //2F
        TokheimReposo,          // 20
        TokheimPorAutorizar,    // A0

        TokheimAutorizado,      // 90
        TokheimDespachoD0,      // D0
        TokheimDespachoF0,      // F0
        TokheimDespacho94,      // 94
        TokheimDespachoD4,      // D4
        TokheimFinDespacho91,
        TokheimFinDespacho95,    //
        TokheimVentaDetenida98,
        TokheimVentaDetenida9C,
        TokheimFinDespacho99,
        TokheimFinDespacho9D,
        TokheimFinDespacho, //DCF no se encontraba
        TokheimReanudarVenta,

        Transicion,
        Teclado,
        DetenidoTemporizador,
        Despacho95,
        DetenidoEnergia,
        DetenidoMonitoreo,
        DetenidoMonitoreo2,
        DetenidoObturador,
        DespachoD4,
        DespachoF0,
        #endregion

        #region Estados agregados para PumpControl (17/03/2009)
        IniciaDespacho,
        Predeterminando,
        EsperandoFinFlujo,
        #endregion

        #region Estados agregados para Cortitec (07/11/2009)
        CoritecReposo,
        CoritecDescolgada,
        CoritecDescolgadaAutorizada,
        CoritecDespachando,
        CoritectFinDeCarga,
        CoritecTimeOut,
        CoritecHaciendoCero,
        CoritecMenu,
        CoritecBateria,
        CoritecCeroFinalizado,
        CoritecLecturaAforador,
        CoritecBloquedo,
        CoritecBloqueado_ON,

        #endregion

        #region Estados de Wayne
        WayneReposo,
        WayneDescolgada,
        WayneDespacho,
        WayneFinDespacho_AF,
        WayneFinDespacho,
        WayneFinDespachoForzado,
        WaynePredeterminada,
        WayneDespachoAutorizado,
        WayneBloqueada,
        #endregion

        #region Estados de Graf.
        GrafError,
        GrafReposo,
        GrafDespacho,
        GrafDescolgada,
        GrafFinDespacho,
        //WaynePredeterminada,
        GrafDespachoAutorizado,
        #endregion


        #region Estados auxiliares (13/07/2009)
        Indeterminado,
        PorReautorizar,
        FinDespachoForzado,
        #endregion


        #region Estados Agregado para 2A Chile (14/04/2011)
        A2Reposo,
        A2Autorizado,
        A2Descolgada,
        A2Despacho,
        A2FinDespacho,
        A2Predeterminada,
        A2DespachoAutorizado,
        A2Bloqueada,
        A2Reset,
        A2PumpNotProgramada,
        A2Predeterminacio_Alcanzada,
        A2Switched_OFF,
        A2DATA,
        A2NACK,
        A2EOT,
        A2ACK,
        #endregion

        #region Estados de Wenzhou (16/01/2012)
        WenzhouModoSistema,
        WenzhouModoManual,
        WenzhouReposo,
        WenzhouDescolgada,
        WenzhouDespacho,
        WenzhouFinDespacho_AF,
        WenzhouFinDespacho,
        WenzhouFinDespachoForzado,
        WenzhouPredeterminada,
        WenzhouDespachoAutorizado,
        WenzhouBloqueada,
        #endregion



        #region Estados de Compac (03/07/2012)
        //IndeterminadoCompac = 0x00,
        //EsperaCompac = 0x30,
        //Autorizada = 0x31,
        //Pre_Despacho = 0x32,
        //Despachando = 0x33,
        //FlujoLento = 0x34,
        //FinalizandoDespachoPredeterminado = 0x35,
        //FinalizandoDespachoNozzle = 0x36,
        //DespachoEnCero = 0x37,
        //FinDespachoPredeterminado = 0x38,
        //CambioPrecioEnProceso = 0x39,
        //FinDespacho = 0x3A,
        //StartUp = 0x3F,
        //ErrorCompac = 0x65,
        //CambiadoFactorK = 0x3E,
        //IdentificadorDisponible = 0x3D,
        //BadCard = 0x21,
        //PinpadAvailable = 0x64

        IndeterminadoCompac,
        EsperaCompac,
        Autorizada,
        Pre_Despacho,
        Despachando,
        FlujoLento,
        FinalizandoDespachoPredeterminado,
        FinalizandoDespachoNozzle,
        DespachoEnCero,
        FinDespachoPredeterminado,
        CambioPrecioEnProceso,
        FinDespacho,
        StartUp,
        ErrorCompac,
        CambiadoFactorK,
        IdentificadorDisponible,
        BadCard,
        PinpadAvailable,

        #endregion

        #region Estado EMR3

        EMR3_Reposo, //Bit 0 - Modo de entrega = No, producto que fluye = No 
        EMR3_En_Carga, //Bit 1 - modo de entrega = Sí, el producto fluye = Sí 
        EMR3_Carga_Pausada, //Bit 2 - Modo de entrega = Sí, el producto fluye = No 
        EMR3_Entraga_completa, //2°- Bit 14 - Entrega completa 
        EMR3_Preset_completo, // 2° - Bit 3 - Parada preestablecida. Se establece cuando la entrega se detuvo después de alcanzar el volumen preestablecido. 
        EMR3_PorAutorizar,
        EMR3_Autorizada,
        EMR3_Preset_Pausado,
        EMR3_Pausado,

        EMR3_Fuga,  //Bit 3 - Modo de entrega = No, producto que fluye = Sí        
        Incorreta_Medicion_EMR3,  //Bit 5 - Posición UI metros correctos. Se establece cuando medidor no puede realizar comando solicitado 
        Error_EMR3, //Bit 6 - Error de metro 
        C_C_Activado_EMR3, //Bit 7 - Establecer si el modo de C & C activada

        #endregion


        
        #region PLC HORNER IMW
        Horner_IN_Pos_HB,
        Horner_System_Alarma,
        Horner_Power_UP,
        Horner_Status_ESD,
        Horner_Status_Alarma,
        Horner_Status_Fueling, 
        Horner_Status_Ready,
        Horner_Out_Pos_HB,
        #   endregion 


         #region H2P_Coptron_safe
        Power_on,
        Modo_Normal,
        Distribution_complete,
        Authorized,
        Distribution,
        Distribution_end,
        Distribution_stop,
        Authorization_denied

        #endregion

    }



    public enum EstadoManguera
    {
        Indeterminado,
        Extraida,
        Colgada
    }

    /*- Fecha de Inclusión: 2008/03/24 13:00 -*/
    //Variables utilizadas para control de surtidores DEVELCO
    public enum StatusPulsador             //Define los ESTADOS del pulador de totales
    {
        Activado,
        Desactivado
    }
    public enum HardwareStatus			    //Define los ESTADOS FISICOS de la Manguera
    {
        Reposo,
        Extraida
    }
    public enum SeriesTokeim
    {
        TOKHEIM262A,
        TOKHEIMPREMIER
    }

    public enum WorkStatus                 //Define los ESTADOS DE CARGA de la Mangueras
    {
        ComienzoCarga,
        EnCarga,
        FinalDeCarga,
        NoEnCarga
    }
    public enum ErrorFlags                 //Define los ERRORES de la manguera
    {
        NoError = 0x00,
        ExcesoFlujo = 0x01,
        AltaPresion = 0x02,
        FallaAlimentacion = 0x03,
        ErrorTotales = 0x04,
        LineaDeshabilitada = 0x05,
        ParadaEmergencia = 0x06,
        PerdidaGas = 0x07,
        ErrorSensorCaudal = 0x08,
        ErrorTeclasTotales = 0x09,
        ErrorTeclasUpDown = 0x0A,
        ErrorSecuenciaProg = 0x0B,
        ErrorEEPROM = 0x0C,
        ErrorChecksum = 0x0D,
        ErrorAritmetico = 0x0E,
        ErrorEscala = 0x0F,
        ErrorParciales = 0x10,
        ErrorNvRAM = 0x11,
        ErrorOCA = 0x12,
        DesLineaActiva = 0x32,
        EsperaHabilita = 0x33,
        #region Estado EMR3
        EMR3_Reposo, //Bit 0 - Modo de entrega = No, producto que fluye = No 
        EMR3_En_Carga, //Bit 1 - modo de entrega = Sí, el producto fluye = Sí 
        EMR3_Carga_Pausada, //Bit 2 - Modo de entrega = Sí, el producto fluye = No 
        EMR3_Entraga_completa, //2°- Bit 14 - Entrega completa 
        EMR3_Preset_completo, // 2° - Bit 3 - Parada preestablecida. Se establece cuando la entrega se detuvo después de alcanzar el volumen preestablecido. 
        EMR3_PorAutorizar,
        EMR3_Autorizada,
        EMR3_Preset_Pausado,
        EMR3_Pausado,

        EMR3_Fuga,  //Bit 3 - Modo de entrega = No, producto que fluye = Sí        
        Incorreta_Medicion_EMR3,  //Bit 5 - Posición UI metros correctos. Se establece cuando medidor no puede realizar comando solicitado 
        Error_EMR3, //Bit 6 - Error de metro 
        C_C_Activado_EMR3 //Bit 7 - Establecer si el modo de C & C activada

        #endregion
    }
    /*---------------------------------------*/


}