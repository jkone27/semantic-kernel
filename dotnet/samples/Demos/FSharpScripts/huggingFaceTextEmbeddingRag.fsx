// TODO: this script does not work yet, but the setup seems almost correct? based on steves anderson C# sample
#r "nuget: Microsoft.Extensions.DependencyInjection"
#r "nuget: Microsoft.Extensions.Http"
#r "nuget: Microsoft.Extensions.Logging.Console"
#r "nuget: Microsoft.Extensions.Logging"
#r "nuget: Microsoft.SemanticKernel.Connectors.HuggingFace, 1.15.0-preview"
#r "nuget: Microsoft.SemanticKernel.Plugins.Memory, 1.15.0-alpha"
#r "nuget: SmartComponents.LocalEmbeddings.SemanticKernel, 0.1.0-preview10148"
#r "nuget: FSharp.Control.TaskSeq"


open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.ChatCompletion
open Microsoft.Extensions.Logging
open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Http.Logging
open System.Net.Http
open System.Net.Http.Json
open Microsoft.Extensions.Http
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
//open Microsoft.KernelMemory ?
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.ChatCompletion
open Microsoft.SemanticKernel.Connectors.HuggingFace
open Microsoft.SemanticKernel.Embeddings
open Microsoft.SemanticKernel.Memory
open Microsoft.SemanticKernel.Plugins.Memory
open FSharp.Control

let API_KEY = "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"
let EMBEDDING_MODEL_ID = "sentence-transformers/all-MiniLM-L6-v2"
let MODEL_ID = "microsoft/Phi-3-mini-4k-instruct"
let API_URL = $"https://api-inference.huggingface.co/" |> Uri


let question = "What is Bruno's favourite super hero?"
Console.WriteLine($"This program will answer the following question: {question}")
Console.WriteLine("1st approach will be to ask the question directly to the Phi-3 model.")
Console.WriteLine("2nd approach will be to add facts to a semantic memory and ask the question again")
Console.WriteLine("")


// Create a chat completion service
let builder = 
    let b = Kernel.CreateBuilder()
    
    b.AddHuggingFaceChatCompletion(
    model= MODEL_ID,
    endpoint= API_URL,
    apiKey= API_KEY) |> ignore
    
    b.AddHuggingFaceTextEmbeddingGeneration(
        model=EMBEDDING_MODEL_ID, 
        endpoint=API_URL, 
        apiKey=API_KEY) |> ignore
    b

let kernel = builder.Build()

Console.WriteLine($"Phi-3 response (no memory).")
let response = 
    kernel.InvokePromptStreamingAsync(question)

let printResponse =
    async {
        for result in response do 
            Console.Write(result)
    }
    |> Async.RunSynchronously

// separator
Console.WriteLine("")
Console.WriteLine("==============")
Console.WriteLine("")

// get the embeddings generator service
let embeddingGenerator = 
    kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>()
let memory = 
    new SemanticTextMemory(new VolatileMemoryStore(), embeddingGenerator)

// add facts to the collection
let MemoryCollectionName = "fanFacts"

let addFacts = 
    task {

        let! _ = memory.SaveInformationAsync(
                MemoryCollectionName, id= "info1", 
                text= "Gisela's favourite super hero is Batman")
        let! _ = memory.SaveInformationAsync(
                MemoryCollectionName, id= "info2", 
                text= "The last super hero movie watched by Gisela was Guardians of the Galaxy Vol 3")
        let! _ = memory.SaveInformationAsync(
                MemoryCollectionName, id= "info3", 
                text= "Bruno's favourite super hero is Invincible")
        let! _ = memory.SaveInformationAsync(
                MemoryCollectionName, id= "info4", 
                text= "The last super hero movie watched by Bruno was Aquaman II")
        let! _ = memory.SaveInformationAsync(
                MemoryCollectionName, id= "info5", 
                text= "Bruno don't like the super hero movie: Eternals")

        ()
    }

let memoryPlugin = new TextMemoryPlugin(memory)

// Import the text memory plugin into the Kernel.
kernel.ImportPluginFromObject(memoryPlugin)

let settings = new HuggingFacePromptExecutionSettings()
//settings.ToolCallBehavior=ToolCallBehavior.AutoInvokeKernelFunctions

let prompt = @"
    Question: {{$input}}
    Answer the question using the memory content: {{Recall}}"

let arguments = new KernelArguments(settings)
arguments.Add("input", question)
arguments.Add("collection", MemoryCollectionName)

Console.WriteLine($"Phi-3 response (using semantic memory).")

response = kernel.InvokePromptStreamingAsync(prompt, arguments)

let printResults = 
    async {
        for result in response do
            Console.Write(result)
    }
    |> Async.RunSynchronously

printfn ""
