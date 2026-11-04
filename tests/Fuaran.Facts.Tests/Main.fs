module Fuaran.Facts.Tests.Main

open Expecto

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv Tests.all
