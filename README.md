# Documentacion
## Modelo de google
https://www.tensorflow.org/hub/tutorials/movenet?hl=es-419

## Descarga de modelo en kaggle
https://www.kaggle.com/models/google/movenet

## Software para manejar modelos onnx y sentis en unity
https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html

## ONNX
https://onnx.ai/onnx/intro/

## RUTIMA DE ONNX
https://onnxruntime.ai/


# Descargar los modelos en formato saved_model de tensorflow

hay tres modelo  lightning (mas rapido pero menos preciso) y thunder (mas preciso, pero mas lento). Uno es multpose.

```python
import kagglehub
# Download latest version
kagglehub.model_download("google/movenet/tensorFlow2/multipose-lightning")
kagglehub.model_download("google/movenet/tensorFlow2/singlepose-lightning")
kagglehub.model_download("google/movenet/tensorFlow2/singlepose-thunder")
```

# Convertimos el modelo a onnx para poder usarlo en Unity con sentis
Lo que descargamos va a parar al directorio .cache (al menos en mac)

```
python -m tf2onnx.convert --saved-model /Users/<usuario>/.cache/kagglehub/models/google/movenet/tensorFlow2/singlepose-thunder/4 --output singlepose-thunder.onnx --opset 15 --tflite                           
```

# Hay que instalar el sentis en unity
https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/install.html


# Situacion de los ficheros
Dentro de unity se puede convertir el modelo onnx a formato .sentis. (esto si funciona)
En el directorio Assets esta el archivo onnx.
En el directorio StreamingAssets se crea el archivo .sentis

# Carga del modelo en Unity en el Start
```csharp
       var PATH = "Assets/StreamingAssets/singlepose-thunder.sentis";
       sourceModel = ModelLoader.Load(PATH);
```

# Inferencia

```csharp
            // tensor 1, 256, 256, 3
            TensorShape formaTensor = new TensorShape(1, texture2D.height, texture2D.width, 3);
            // 5. Crear el ITensor a partir del array de datos y la forma
            Tensor tensorEntrada = new Tensor<int>(formaTensor, datosTensor);
            // 6. Inferencia
            worker.Schedule(tensorEntrada);
            // el tensor de salida es 1, 1, 1, 17, 3 
            // 17 es el numero de keypoints
            // 3 es el numero de coordenadas (x, y, score)
            // Get the output tensor from the model
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
```


# Estado del proyecto

La situación es que no se dibujan bien los puntos. Es posible que no esté bien hecho el tensor de entrada.
Habría que analizar el resultado de la inferencia para ver si está bien hecha.
Cuando lo logremos podemos usar el mismo código para comparar la precisión y tiempos de ligthing (singlepose) y thunder.



