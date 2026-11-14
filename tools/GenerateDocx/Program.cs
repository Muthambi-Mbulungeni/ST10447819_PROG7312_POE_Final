using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace GenerateDocx
{
    class Program
    {
        static void Main(string[] args)
        {
            string? solutionRoot = AppDomain.CurrentDomain.BaseDirectory;
            while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot, "SmartX.sln")))
                solutionRoot = Directory.GetParent(solutionRoot)?.FullName;
            solutionRoot ??= AppDomain.CurrentDomain.BaseDirectory;

            string outputDir = Path.Combine(solutionRoot, "docs");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string docxPath = Path.Combine(outputDir, "SmartX_Part3_Final_Comprehensive_Report.docx");

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                // Title Page Header
                AddParagraph(body, "SMART-X INDUSTRIAL IOT PLATFORM: FINAL PROJECT COMPLETION & TECHNICAL REPORT", true, 30, 240);
                AddParagraph(body, "Module: PROG7312 — Advanced Programming | PoE Part 3 (Final Full Functioning App - 100 Marks)", false, 22, 120, true);
                AddParagraph(body, "Student Name: Muthambi Mbulungeni | Student Number: ST10447819 | The Independent Institute of Education (IIE) / Varsity College", false, 20, 260, true);

                // Section 1: Executive Summary
                AddHeading(body, "1. Executive Summary & Project Ecosystem Architecture");
                AddBodyParagraph(body, "The Smart-X Industrial IoT Platform represents an enterprise-grade distributed edge-to-cloud ecosystem developed to monitor, orchestrate, and optimize dispersed fleets of ESP32 microcontrollers operating across mission-critical facilities. Throughout the three progressive milestones, the architecture was systematically scaled from foundational ingestion mechanisms to decentralized mesh topology management:");
                AddBullet(body, "Part 1 (Sensor Ingestion & Telemetry): Implemented generic TelemetryPacket<T> zero-boxing models, operator overloading, raw jagged array batch ingestion, recursive deployment tree validation, and AES-256 encrypted diagnostic attachments.");
                AddBullet(body, "Part 2 (Real-Time Command Stream & History): Developed the dedicated SmartXDataStructures container class featuring Queue<T>, PriorityQueue, Stack<T> with 1-click Undo, Dictionary O(1), SortedDictionary chronological logging, HashSet disconnect deduplication, Supabase Cloud Auth, and automated predictive recommendations.");
                AddBullet(body, "Part 3 (Network Topology & Mesh Routing): Mathematically modeled physical wireless mesh networks using an undirected weighted MeshGraph, custom self-balancing AVLTree<TKey, TValue> for O(log N + K) range queries, binary MinHeap, Prim's Minimum Spanning Tree algorithm, BFS/DFS traversal, and dynamic self-healing routing failover.");

                // Section 2: Implementation Report & Tree Data Structures
                AddHeading(body, "2. Implementation Report: Tree Structures & Historical Telemetry Range Querying (30 Marks)");
                AddBodyParagraph(body, "A prominent challenge in enterprise IoT platforms is the efficient indexing and retrieval of timestamped historical telemetry. When thousands of sensors publish continuous telemetry, storing records in naive unorganized arrays or linked lists incurs O(N) linear scan time per query. Furthermore, simple Binary Search Trees (BST) run the critical risk of degenerating into an O(N) linked list when incoming telemetry packets arrive in strictly ascending chronological order.");
                AddBodyParagraph(body, "To solve this degradation, Smart-X implements a custom, from-scratch self-balancing AVL Tree (AVLTree<TKey, TValue>). The AVL Tree enforces the strict height-balance property where the height difference between the left and right subtrees of any node is at most 1 (|Balance Factor| <= 1). Whenever an insertion causes an imbalance, the tree automatically performs deterministic rotations: Left-Left, Right-Right, Left-Right, or Right-Left.");
                AddBodyParagraph(body, "The primary algorithmic contribution of our AVL Tree is its logarithmic range querying method (QueryRange(fromUtc, toUtc)). Rather than inspecting the entire dataset, the algorithm leverages binary search properties to prune out-of-bounds subtrees: if the current node's timestamp is less than the lower bound, the entire left subtree is discarded; if greater than the upper bound, the entire right subtree is discarded. This achieves an optimal time complexity of O(log N + K), where N is the total number of readings and K is the count of records within the window.");

                AddHeading(body, "3. Implementation Report: Graphs, Traversal, Heaps, and Minimum Spanning Trees (30 Marks)");
                AddBodyParagraph(body, "In Part 3, the IoT physical and wireless communication fabric is modeled as a weighted undirected graph G = (V, E) inside MeshGraph.cs:");
                AddBullet(body, "Vertices (V): Represent physical nodes, including ESP32 environmental sensors, zone repeaters, and the Central Gateway Hub.");
                AddBullet(body, "Edges (E): Represent wireless links (ESP-NOW, Zigbee, Thread) weighted by transmission latency (in milliseconds), radio-frequency signal attenuation (RSSI in dBm), and power consumption metrics.");
                AddBodyParagraph(body, "Subnet Reachability (BFS & DFS): To verify that devices are online and discover which subnet cluster they belong to, the Central Gateway executes Breadth-First Search (BFS) and Depth-First Search (DFS). BFS explores devices layer-by-layer, making it ideal for hop-distance auditing and identifying partitioned or unreachable devices. DFS recursively explores cluster boundaries and cycle structures.");
                AddBodyParagraph(body, "Minimum Spanning Tree (Prim's Algorithm with Min-Heap): Battery life is the primary constraint of field-deployed wireless microcontrollers. Broadcasting at maximum power directly to a distant gateway drains batteries and causes packet collisions. Smart-X employs Prim's Algorithm backed by our custom MinHeap<TElement, TPriority> to construct the Minimum Spanning Tree (MST). Starting at the Central Gateway, the algorithm greedily adds the minimum-weight wireless link that connects an unvisited node, establishing the global power-optimal backbone spanning all V nodes across V - 1 links in O(E log V) time.");
                AddBodyParagraph(body, "Dynamic Mesh Self-Healing (Dijkstra Failover): To replicate physical mesh resilience, the platform provides Dijkstra shortest path route tracing with self-healing failover. When an operator or physical fault disables a repeater (e.g. Repeater Alpha goes offline), the routing engine automatically avoids the failed node and dynamically discovers alternate redundant paths through neighboring repeaters without service interruption.");

                // Section 3: Project Completion Report
                AddHeading(body, "4. Project Completion Report: Architectural Roadblocks, Debugging & Learnings (10 Marks)");
                AddBodyParagraph(body, "Integrating an advanced .NET 10 backend with a responsive, client-side HTML5/Canvas visualization suite revealed multiple architectural hurdles:");
                AddBullet(body, "Thread-Safe Concurrency: High-throughput HTTP ingestion concurrently modified telemetry trees while operators queried MST topology. We resolved this by implementing clean lock synchronization primitives and returning immutable projections.");
                AddBullet(body, "Canvas Math & Coordinate Mapping: Graph vertices and edge vectors were mapped from spatial coordinates to an HTML5 Canvas with dynamic 60 FPS animations, node pulses, and hover hit-testing.");
                AddBullet(body, "Verification of Asymptotic Bounds: To guarantee the AVL Tree maintains O(log N) depth under chronological inputs, we authored unit tests verifying that 60 chronological insertions maintain a maximum tree height <= 5.");

                // Section 4: Technology Recommendations & Future Scaling
                AddHeading(body, "5. Technology Recommendations & Future Architectural Scaling (10 Marks)");
                AddBodyParagraph(body, "To scale the Smart-X ecosystem from high-performance prototype to planet-scale production, we recommend the following strategic technologies:");
                AddBullet(body, "Distributed Cloud Hosting (Google Cloud / Azure): Deploy containerized API microservices onto Google Cloud Run or Azure App Service, persisting multi-million telemetry streams to distributed time-series databases like TimescaleDB or Google Bigtable.");
                AddBullet(body, "CI/CD Automation via GitHub Actions: Automate build verification, security scanning, unit testing (dotnet test), and Docker container registry image creation on every commit.");
                AddBullet(body, "LLM Anomaly Triage (Google Gemini API): Embed an LLM API to ingest telemetry variance logs and generate plain-English diagnostic summaries and action checklists for field technicians.");
                AddBullet(body, "Home Assistant & Industrial MQTT Gateway: Expose an MQTT bridge to enable plug-and-play interoperability with Home Assistant (https://www.home-assistant.io/) and industrial SCADA controllers.");

                // Section 5: Iterative Feedback Ledger
                AddHeading(body, "6. Iterative Feedback Incorporation & Verification Ledger (5 Marks)");
                AddBodyParagraph(body, "All lecturer feedback items across Part 1 and Part 2 were formally audited and integrated into the suite (documented in docs/FEEDBACK_UPDATES_LOG.md):");
                AddBullet(body, "Explicit Docker and multi-stage container deployment instructions added to README.md.");
                AddBullet(body, "Dedicated container class SmartXDataStructures.cs populated with named properties and expanded for Part 3 AVLTree and MeshGraph.");
                AddBullet(body, "Live Chart.js telemetry waveform and gauges integrated to eliminate static manual refreshing.");
                AddBullet(body, "Batch .txt device provisioning implemented with regex validation and instant MAC deduplication.");
                AddBullet(body, "Real cloud authentication connected using Supabase Cloud Auth API.");
                AddBullet(body, "All 3 main menu architectural pillars fully unlocked and interconnected.");

                mainPart.Document.Save();
            }

            Console.WriteLine($"Successfully generated Final Part 3 Word Report at: {docxPath}");
        }

        private static void AddHeading(Body body, string text)
        {
            Paragraph p = body.AppendChild(new Paragraph());
            ParagraphProperties pPr = p.AppendChild(new ParagraphProperties());
            pPr.SpacingBetweenLines = new SpacingBetweenLines() { Before = "240", After = "80" };
            Run r = p.AppendChild(new Run());
            r.AppendChild(new RunProperties(new Bold(), new FontSize() { Val = "24" }, new Color() { Val = "1F4E79" }));
            r.AppendChild(new Text(text));
        }

        private static void AddParagraph(Body body, string text, bool bold, int fontSize, int spacingAfter, bool italic = false)
        {
            Paragraph p = body.AppendChild(new Paragraph());
            ParagraphProperties pPr = p.AppendChild(new ParagraphProperties());
            pPr.SpacingBetweenLines = new SpacingBetweenLines() { After = spacingAfter.ToString(), Line = "280", LineRule = LineSpacingRuleValues.Auto };
            Run r = p.AppendChild(new Run());
            RunProperties rPr = r.AppendChild(new RunProperties(new FontSize() { Val = fontSize.ToString() }));
            if (bold) rPr.AppendChild(new Bold());
            if (italic) rPr.AppendChild(new Italic());
            r.AppendChild(new Text(text));
        }

        private static void AddBodyParagraph(Body body, string text)
        {
            Paragraph p = body.AppendChild(new Paragraph());
            ParagraphProperties pPr = p.AppendChild(new ParagraphProperties());
            pPr.SpacingBetweenLines = new SpacingBetweenLines() { After = "120", Line = "280", LineRule = LineSpacingRuleValues.Auto };
            Run r = p.AppendChild(new Run());
            r.AppendChild(new RunProperties(new FontSize() { Val = "22" }));
            r.AppendChild(new Text(text));
        }

        private static void AddBullet(Body body, string text)
        {
            Paragraph p = body.AppendChild(new Paragraph());
            ParagraphProperties pPr = p.AppendChild(new ParagraphProperties());
            pPr.Indentation = new Indentation() { Left = "360" };
            pPr.SpacingBetweenLines = new SpacingBetweenLines() { After = "60" };
            Run r = p.AppendChild(new Run());
            r.AppendChild(new RunProperties(new FontSize() { Val = "22" }));
            r.AppendChild(new Text("• " + text));
        }
    }
}
