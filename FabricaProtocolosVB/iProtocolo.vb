Public Interface iProtocolo
    Sub Evento_CerrarProtocolo()
    Sub Evento_FinalizarCambioTarjeta(Cara As Byte)
    Sub Evento_FinalizarVentaPorMonitoreoCHIP(Cara As Byte)
    Sub Evento_InactivarCaraCambioTarjeta(Cara As Byte, Puerto As String)
    Sub Evento_TurnoAbierto(Surtidores As String, PuertoTerminal As String, Precios As System.Array)
    Sub Evento_TurnoCerrado(Surtidores As String, PuertoTerminal As String)
    Sub Evento_VentaAutorizada(Cara As Byte, Precio As String, ValorProgramado As String, TipoProgramacion As Byte, Placa As String, MangueraProgramada As Int32, EsVentaGerenciada As Boolean, guid As String, PresionLLenado As Decimal)
    'Sub Evento_VentaAutorizada(Cara As Byte, Precio As String, ValorProgramado As String, TipoProgramacion As Byte, Placa As String, MangueraProgramada As Int32, EsVentaGerenciada As Boolean, guid As String)

    Sub Evento_ProgramarCambioPrecioKardex(mangueras As ColMangueras)
    Sub Evento_Predeterminar(Cara As Byte, ValorProgramado As String, TipoProgramacion As Byte)
    Sub Evento_CancelarVenta(Cara As Byte)
    Sub SolicitarLecturasSurtidor(ByRef Lectura As String, Surtidor As String) 'este evento es para solicitar lecturas por surtidor DCF 11/12/2017



    Event AutorizacionRequerida(Cara As Byte, Producto As Long, idManguera As Long, Lectura As String, guid As String)
    ''  Event AutorizacionRequeridaGuid(Cara As Byte, Producto As Long, idManguera As Long, Lectura As String)
    Event CambioMangueraEnVentaGerenciada(Cara As Byte)
    Event CambioPrecioFallido(idManguera As Long, Precio As Double)
    Event CancelarProcesarTurno(Cara As Byte, Mensaje As String, EstadoTurno As Boolean)
    Event CaraEnReposo(Cara As Byte, idManguera As Long)
    Event ExcepcionOcurrida(Mensaje As String, Imprime As Boolean, Terminal As Boolean, puerto As String)
    Event IniciarCambioTarjeta(Cara As Byte, puerto As String)
    Event LecturaInicialVenta(Cara As Byte, Lectura As String)
    Event LecturasCambioTarjeta(Lectura As System.Array)
    Event LecturaTurnoAbierto(Lectura As System.Array)
    Event LecturaTurnoCerrado(Lectura As System.Array)
    Event VentaFinalizada(Cara As Byte, Valor As String, Precio As String, LecturaFinal As String, Cantidad As String, Producto As String, manguera As Long, presionLLenado As String, lecturaInicial As String)
    Event NotificarCambioPrecioManguera(manguera As Long)
    Event VentaInterrumpidaEnCero(Cara As Byte)
    Event VentaParcial(Cara As Byte, Valor As String, Cantidad As String)

End Interface
