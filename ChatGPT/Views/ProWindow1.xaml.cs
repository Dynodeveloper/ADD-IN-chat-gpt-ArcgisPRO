using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using Microsoft.Win32;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System.IO;
using System.Windows;
using System;
using OpenAI_API.Models;
using OpenAI_API.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Internal.Geometry;
using System.Text.RegularExpressions;
using System.Linq;

namespace ChatGPT.Views
{
    public partial class ProWindow1
    {
        private List<ChatMessage> chatMessages = new List<ChatMessage>();

        public ProWindow1()
        {
            InitializeComponent();
            ConfigureAndInitialize();
            btnUpload.Click += btnUpload_Click;
            btnSend.Click += btnSend_Click;
        }

        private void ConfigureAndInitialize()
        {
            // Inicializa el API de OpenAI aquí si es necesario
        }

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Archivos PDF (*.pdf)|*.pdf";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string extractedText = ConvertFileToText(filePath);

                // Divide el contenido en párrafos
                string[] paragraphs = extractedText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Agregar cada párrafo como un mensaje de sistema individual
                foreach (var paragraph in paragraphs)
                {
                    // Agregar el párrafo como mensaje de sistema
                    chatMessages.Add(new ChatMessage(ChatMessageRole.System, paragraph));

                    // Esperar un breve tiempo para dar tiempo al modelo de procesar el mensaje
                    await Task.Delay(2000);
                }

                // Habilitar el envío de preguntas al modelo
                btnSend.IsEnabled = true;
            }
        }


        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string inputText = txtMessageInput.Text;

            // Agregar la pregunta del usuario como mensaje del usuario
            chatMessages.Add(new ChatMessage(ChatMessageRole.User, inputText));

            // Obtener la respuesta del modelo
            string generatedResponse = await GetModelResponse(inputText);

            // Agregar la respuesta del modelo como mensaje del modelo
            chatMessages.Add(new ChatMessage(ChatMessageRole.Assistant, generatedResponse));

            // Actualizar la interfaz de usuario
            txtResponses.Text += generatedResponse + Environment.NewLine;

            // Limpiar el cuadro de entrada
            txtMessageInput.Clear();
        }

        private async Task<string> GetModelResponse(string userMessage)
        {
            var api = new OpenAI_API.OpenAIAPI("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");

            // Crear una lista de mensajes con los mensajes actuales
            var messages = new List<ChatMessage>(chatMessages);
            messages.Add(new ChatMessage(ChatMessageRole.User, userMessage));

            var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest()
            {
                Model = Model.ChatGPTTurbo0301,
                Messages = messages.ToArray()
            });

            return chatResult.Choices[0].Message.Content;
        }

        private async void btnPlaceMarker_Click(object sender, RoutedEventArgs e)
        {
            // Obtener la última respuesta del modelo
            string lastResponse = chatMessages.LastOrDefault(m => m.Role == ChatMessageRole.Assistant)?.Content;

            if (!string.IsNullOrEmpty(lastResponse))
            {
                // Verificar si la respuesta contiene coordenadas
                if (TryParseCoordinates(lastResponse, out MapPoint mapPoint))
                {
                    // Colocar un marcador en ArcGIS Pro con las coordenadas proporcionadas
                    await QueuedTask.Run(() =>
                    {
                        // Obtiene el mapa activo
                        var mapView = MapView.Active;
                        if (mapView != null)
                        {
                            // Crea un marcador y lo agrega al mapa
                            mapView.AddOverlay(mapPoint);
                        }
                    });

                    // Actualizar la interfaz de usuario
                    txtResponses.Text += "Se colocó un marcador en las coordenadas: " + mapPoint.X + ", " + mapPoint.Y + Environment.NewLine;
                }
                else
                {
                    // Actualizar la interfaz de usuario en caso de que las coordenadas no se puedan obtener
                    txtResponses.Text += "No se pudieron obtener las coordenadas para colocar el marcador." + Environment.NewLine;
                }
            }
            else
            {
                // Mostrar un mensaje en la interfaz si no hay respuesta previa del modelo
                txtResponses.Text += "No hay respuesta previa del modelo para obtener coordenadas." + Environment.NewLine;
            }
        }



        private string ConvertFileToText(string filePath)
        {
            if (Path.GetExtension(filePath).ToLower() == ".pdf")
            {
                return ConvertPdfToText(filePath);
            }
            else
            {
                return File.ReadAllText(filePath);
            }
        }

        private bool TryParseCoordinates(string input, out MapPoint mapPoint)
        {
            // Intentar encontrar patrón de coordenadas en el formato "Latitud: X, Longitud: Y"
            var regex = new Regex(@"Latitud: (?<lat>-?\d+(\.\d+)?), Longitud: (?<lon>-?\d+(\.\d+)?)");
            var match = regex.Match(input);

            if (match.Success)
            {
                if (double.TryParse(match.Groups["lat"].Value, out double latitude) && double.TryParse(match.Groups["lon"].Value, out double longitude))
                {
                    // Crear un MapPoint con las coordenadas obtenidas
                    mapPoint = MapPointBuilder.CreateMapPoint(longitude, latitude, SpatialReferences.WGS84);
                    return true;
                }
            }
            else
            {
                // Intentar analizar las coordenadas en el formato directo "latitud, longitud"
                var coordinatesParts = input.Split(',');
                if (coordinatesParts.Length == 2 &&
                    double.TryParse(coordinatesParts[0], out double latitude) &&
                    double.TryParse(coordinatesParts[1], out double longitude))
                {
                    // Crear un MapPoint con las coordenadas obtenidas
                    mapPoint = MapPointBuilder.CreateMapPoint(longitude, latitude, SpatialReferences.WGS84);
                    return true;
                }
            }

            // Buscar patrón de coordenadas en la parte final del texto
            var finalRegex = new Regex(@"(-?\d+(\.\d+)?), (-?\d+(\.\d+)?)$");
            var finalMatch = finalRegex.Match(input);

            if (finalMatch.Success)
            {
                if (double.TryParse(finalMatch.Groups[1].Value, out double latitude) && double.TryParse(finalMatch.Groups[3].Value, out double longitude))
                {
                    // Crear un MapPoint con las coordenadas obtenidas
                    mapPoint = MapPointBuilder.CreateMapPoint(longitude, latitude, SpatialReferences.WGS84);
                    return true;
                }
            }

            mapPoint = null;
            return false;
        }



        private void btnExportCreateTable_Click(object sender, RoutedEventArgs e)
        {
            // Obtener la última respuesta del modelo
            string lastResponse = chatMessages.LastOrDefault(m => m.Role == ChatMessageRole.Assistant)?.Content;

            if (!string.IsNullOrEmpty(lastResponse))
            {
                // Crear o exportar la tabla a un archivo CSV
                try
                {
                    string[] rows = lastResponse.Split('\n'); // Separar las filas de la respuesta
                    List<string[]> tableData = new List<string[]>();

                    foreach (string row in rows)
                    {
                        string[] rowData = row.Split(','); // Separar los datos de cada fila
                        tableData.Add(rowData);
                    }

                    // Crear un archivo CSV y escribir los datos
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string csvFilePath = Path.Combine(desktopPath, "Tabla.csv");

                    // Ruta donde se guardará el archivo CSV

                    using (StreamWriter writer = new StreamWriter(csvFilePath))
                    {
                        foreach (var rowData in tableData)
                        {
                            writer.WriteLine(string.Join(",", rowData));
                        }
                    }

                    txtResponses.Text += "Tabla exportada correctamente a: " + csvFilePath + Environment.NewLine;
                }
                catch (Exception ex)
                {
                    txtResponses.Text += "Error al exportar la tabla: " + ex.Message + Environment.NewLine;
                }
            }
            else
            {
                txtResponses.Text += "No hay respuesta del modelo para crear la tabla." + Environment.NewLine;
            }
        }



        private string ConvertPdfToText(string pdfFilePath)
        {
            using (PdfDocument pdfDocument = new PdfDocument(new PdfReader(pdfFilePath)))
            {
                StringWriter text = new StringWriter();
                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                    PdfCanvasProcessor parser = new PdfCanvasProcessor(strategy);
                    parser.ProcessPageContent(pdfDocument.GetPage(i));
                    text.WriteLine(strategy.GetResultantText());
                }
                return text.ToString();
            }
        }

        
 private async void btnCreatePolygon_Click(object sender, RoutedEventArgs e)
        {
            // Obtener el mapa activo
            var mapView = MapView.Active;
            if (mapView != null)
            {
                // Obtener la última respuesta del modelo
                string lastResponse = chatMessages.LastOrDefault(m => m.Role == ChatMessageRole.Assistant)?.Content;

                if (!string.IsNullOrEmpty(lastResponse))
                {
                    // Verificar si la respuesta es solo "Antioquia"
                    if (lastResponse.Trim().Equals("Antioquia", StringComparison.OrdinalIgnoreCase))
                    {
                        // Si la respuesta es solo "Antioquia", seleccionar el departamento completo
                        await SelectDepartment("Antioquia", mapView);
                        return; // Salir de la función
                    }

                    // Continuar con la lógica para otros nombres
                    List<string> namesToSelect = new List<string>();

                    // Supongamos que la respuesta contiene los nombres separados por comas
                    string[] responseParts = lastResponse.Split(',');

                    foreach (var part in responseParts)
                    {
                        // Limpiar y agregar nombres a la lista de selección
                        string cleanedName = part.Trim();
                        if (!string.IsNullOrEmpty(cleanedName))
                        {
                            namesToSelect.Add(cleanedName);
                        }
                    }

                    if (namesToSelect.Count > 0)
                    {
                        // Realizar una selección basada en los nombres encontrados
                        await SelectNames(namesToSelect, mapView);
                    }
                    else
                    {
                        txtResponses.Text += "No se encontraron nombres en la respuesta." + Environment.NewLine;
                    }
                }
                else
                {
                    // Mostrar un mensaje en la interfaz si no hay respuesta del modelo
                    txtResponses.Text += "No hay respuesta del modelo para crear la selección." + Environment.NewLine;
                }
            }
            else
            {
                // Mostrar un mensaje en la interfaz si no hay un mapa activo
                txtResponses.Text += "No hay un mapa activo." + Environment.NewLine;
            }
        }

        private async Task SelectDepartment(string departmentName, MapView mapView)
        {
            await QueuedTask.Run(() =>
            {
                // Obtener la capa de departamentos
                var departmentLayer = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(layer => layer.Name == "DatosADDIN");

                if (departmentLayer != null)
                {
                    // Crear una expresión de selección para el departamento
                    var departmentSelection = new ArcGIS.Core.Data.QueryFilter();
                    departmentSelection.WhereClause = $"NOM_DEP = '{departmentName}'";

                    // Seleccionar el departamento
                    departmentLayer.Select(departmentSelection);
                }
            });

            // Actualizar la interfaz de usuario o realizar cualquier otra acción necesaria
            txtResponses.Text += $"Se ha seleccionado el departamento: {departmentName}." + Environment.NewLine;
        }

        private async Task SelectNames(List<string> namesToSelect, MapView mapView)
        {
            await QueuedTask.Run(() =>
            {
                // Obtener la capa "Veredas de Colombia"
                var veredasTable = mapView.Map.GetLayersAsFlattenedList().OfType<StandaloneTable>().FirstOrDefault(layer => layer.Name == "Veredas de Colombia");

                if (veredasTable != null)
                {
                    // Crear una expresión de selección para los nombres
                    var selectionExpression = $"NOM_DEP IN ({string.Join(",", namesToSelect.Select(name => $"'{name}'"))}) OR " +
                                              $"NOMB_MPIO IN ({string.Join(",", namesToSelect.Select(name => $"'{name}'"))}) OR " +
                                              $"NOMBRE_VER IN ({string.Join(",", namesToSelect.Select(name => $"'{name}'"))})";

                    // Crear un filtro de consulta
                    var queryFilter = new ArcGIS.Core.Data.QueryFilter();
                    queryFilter.WhereClause = selectionExpression;

                    // Realizar la selección en la capa (tabla)
                    var selection = veredasTable.Select(queryFilter);

                    // Verificar si se seleccionaron registros
                    if (selection.GetCount() > 0)
                    {
                        // Actualizar la interfaz de usuario o realizar cualquier otra acción necesaria
                        txtResponses.Text += "Se han seleccionado elementos en la capa 'Veredas de Colombia'." + Environment.NewLine;
                    }
                    else
                    {
                        txtResponses.Text += "No se encontraron elementos en la capa 'Veredas de Colombia' que coincidan con la respuesta." + Environment.NewLine;
                    }
                }
            });
        }







        //  private void btnCreateGraph_Click(object sender, RoutedEventArgs e)
        // {
        // Obtener la última respuesta del modelo
        //   string lastResponse = chatMessages.LastOrDefault(m => m.Role == ChatMessageRole.Assistant)?.Content;

        // if (!string.IsNullOrEmpty(lastResponse))
        //{
        // Crear un gráfico a partir de la respuesta del modelo
        //  var graph = new ArcGIS.Desktop.Mapping.Chart();
        //graph.Series.Add(new ArcGIS.Desktop.Mapping.Series("Series 1"));

        // Agregar datos al gráfico
        // ...

        // Colocar el gráfico en el mapa
        // var mapView = MapView.Active;
        //if (mapView != null)
        //{
        // Obtener los 10 números más grandes del documento PDF
        //  var numbers = ConvertPdfToText(lastResponse).Split('\n').Select(n => Convert.ToInt32(n)).OrderByDescending(n => n).Take(10).ToList();

        // Agregar los datos al gráfico
        //  graph.Series[0].Points.Add(new ArcGIS.Desktop.Mapping.ChartPoint(numbers[0], 1));
        //graph.Series[0].Points.Add(new ArcGIS.Desktop.Mapping.ChartPoint(numbers[1], 2));


        // Colocar el gráfico en el mapa
        // mapView.AddOverlay(graph);
        //           }
        //         }
        //     else
        // {
        //       txtResponses.Text += "No hay respuesta del modelo para crear el gráfico." + Environment.NewLine;
        //}
        //}

        //private void PlaceGraphicOnMap(CIMGraphic graphic)
        //{
        // Obtiene el mapa activo
        //  var mapView = MapView.Active;
        // if (mapView != null)
        //{
        // Agrega el gráfico al mapa
        //  mapView.AddOverlay(graphic);
        //}
        //}



    }
}
