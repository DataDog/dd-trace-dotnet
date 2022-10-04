// <copyright file="DebuggerSnapshotCreatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
#pragma warning disable CS0414

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class DebuggerSnapshotCreatorTests
    {
        [Fact]
        public async Task Limits_LargeCollection()
        {
            await ValidateSingleValue(Enumerable.Range(1, 2000).ToArray());
        }

        [SkippableFact]
        public async Task Limits_LargeDictionary()
        {
            if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.MacOS)
            {
                throw new SkipException("This test fails only on MacOS, but it's not clear why. It's not a high priority to investigate, so we're skipping it for now.");
            }

            await ValidateSingleValue(Enumerable.Range(1, 2000).ToDictionary(k => k.ToString(), k => k));
        }

        [Fact]
        public async Task Limits_FieldsCount()
        {
            await ValidateSingleValue(new ClassWithLotsOFields());
        }

        [Fact]
        public async Task Limits_StringLength()
        {
            await ValidateSingleValue(new string('f', 5000));
        }

        [Fact]
        public async Task Limits_Depth()
        {
            await ValidateSingleValue(new InfiniteRecursion());
        }

        [Fact]
        public async Task ObjectStructure_Null()
        {
            await ValidateSingleValue(null);
        }

        [Fact]
        public async Task ObjectStructure_EmptyArray()
        {
            await ValidateSingleValue(new int[] { });
        }

        [Fact]
        public async Task ObjectStructure_EmptyList()
        {
            await ValidateSingleValue(new List<int>());
        }

        [Fact]
        public async Task SpecialType_StringBuilder()
        {
            await ValidateSingleValue(new StringBuilder("hi from stringbuilder"));
        }

        [Fact]
        public async Task SpecialType_LazyUninitialized()
        {
            await ValidateSingleValue(new Lazy<int>(() => Math.Max(1, 2)));
        }

        [Fact]
        public async Task SpecialType_LazyInitialized()
        {
            var lazy = new Lazy<int>(() => Math.Max(1, 2));
            var temp = lazy.Value;
            await ValidateSingleValue(lazy);
        }

        /// <summary>
        /// Generate a debugger snapshot by simulating the same flow of method calls as our instrumentation produces for a method probe.
        /// </summary>
        private static string GenerateSnapshot(object instance, object[] args, object[] locals)
        {
            var snapshotCreator = new DebuggerSnapshotCreator();
            snapshotCreator.StartDebugger();
            snapshotCreator.StartSnapshot();
            snapshotCreator.StartCaptures();
            {
                // method entry
                snapshotCreator.StartEntry();
                if (instance != null)
                {
                    snapshotCreator.CaptureInstance(instance, instance.GetType());
                }

                for (var i = 0; i < args.Length; i++)
                {
                    snapshotCreator.CaptureArgument(args[i], "arg" + i, args[i].GetType());
                }

                snapshotCreator.EndEntry(hasArgumentsOrLocals: args.Length > 0);
            }

            {
                // method exit
                snapshotCreator.StartReturn();
                for (var i = 0; i < locals.Length; i++)
                {
                    snapshotCreator.CaptureLocal(locals[i], "local" + i, locals[i]?.GetType() ?? typeof(object));
                }

                for (var i = 0; i < args.Length; i++)
                {
                    snapshotCreator.CaptureArgument(args[i], "arg" + i, args[i].GetType());
                }

                if (instance != null)
                {
                    snapshotCreator.CaptureInstance(instance, instance.GetType());
                }

                snapshotCreator.MethodProbeEndReturn(hasArgumentsOrLocals: args.Length + locals.Length > 0);
            }

            snapshotCreator.FinalizeSnapshot(Array.Empty<StackFrame>(), "Foo", "Bar", DateTimeOffset.MinValue, "foo");

            var snapshot = snapshotCreator.GetSnapshotJson();
            return JsonPrettify(snapshot);
        }

        private static string JsonPrettify(string json)
        {
            using (var stringReader = new StringReader(json))
            using (var stringWriter = new StringWriter())
            {
                var jsonReader = new JsonTextReader(stringReader);
                var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
                jsonWriter.WriteToken(jsonReader);
                return stringWriter.ToString();
            }
        }

        /// <summary>
        /// Validate that we produce valid json for a specific value, and that the output conforms to the given set of limits on capture.
        /// </summary>
        private async Task ValidateSingleValue(object local)
        {
            var snapshot = GenerateSnapshot(null, new object[] { }, new object[] { local });

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            var localVariableAsJson = JObject.Parse(snapshot).SelectToken("debugger.snapshot.captures.return.locals");
            await Verifier.Verify(localVariableAsJson, verifierSettings);
        }

        private class InfiniteRecursion
        {
            private int number = 666;
            private InfiniteRecursion soInfinite;

            public InfiniteRecursion()
            {
                soInfinite = this;
                Console.Write(number);
            }
        }

        private class ClassWithLotsOFields
        {
            private readonly int _numField1 = 1;
            private readonly int _numField2 = 2;
            private readonly int _numField3 = 3;
            private readonly int _numField4 = 4;
            private readonly int _numField5 = 5;
            private readonly int _numField6 = 6;
            private readonly int _numField7 = 7;
            private readonly int _numField8 = 8;
            private readonly int _numField9 = 9;
            private readonly int _numField10 = 10;
            private readonly int _numField11 = 11;
            private readonly int _numField12 = 12;
            private readonly int _numField13 = 13;
            private readonly int _numField14 = 14;
            private readonly int _numField15 = 15;
            private readonly int _numField16 = 16;
            private readonly int _numField17 = 17;
            private readonly int _numField18 = 18;
            private readonly int _numField19 = 19;
            private readonly int _numField20 = 20;
            private readonly int _numField21 = 21;
            private readonly int _numField22 = 22;
            private readonly int _numField23 = 23;
            private readonly int _numField24 = 24;
            private readonly int _numField25 = 25;
            private readonly int _numField26 = 26;
            private readonly int _numField27 = 27;
            private readonly int _numField28 = 28;
            private readonly int _numField29 = 29;
            private readonly int _numField30 = 30;
            private readonly int _numField31 = 31;
            private readonly int _numField32 = 32;
            private readonly int _numField33 = 33;
            private readonly int _numField34 = 34;
            private readonly int _numField35 = 35;
            private readonly int _numField36 = 36;
            private readonly int _numField37 = 37;
            private readonly int _numField38 = 38;
            private readonly int _numField39 = 39;
            private readonly int _numField40 = 40;
            private readonly int _numField41 = 41;
            private readonly int _numField42 = 42;
            private readonly int _numField43 = 43;
            private readonly int _numField44 = 44;
            private readonly int _numField45 = 45;
            private readonly int _numField46 = 46;
            private readonly int _numField47 = 47;
            private readonly int _numField48 = 48;
            private readonly int _numField49 = 49;
            private readonly int _numField50 = 50;
            private readonly int _numField51 = 51;
            private readonly int _numField52 = 52;
            private readonly int _numField53 = 53;
            private readonly int _numField54 = 54;
            private readonly int _numField55 = 55;
            private readonly int _numField56 = 56;
            private readonly int _numField57 = 57;
            private readonly int _numField58 = 58;
            private readonly int _numField59 = 59;
            private readonly int _numField60 = 60;
            private readonly int _numField61 = 61;
            private readonly int _numField62 = 62;
            private readonly int _numField63 = 63;
            private readonly int _numField64 = 64;
            private readonly int _numField65 = 65;
            private readonly int _numField66 = 66;
            private readonly int _numField67 = 67;
            private readonly int _numField68 = 68;
            private readonly int _numField69 = 69;
            private readonly int _numField70 = 70;
            private readonly int _numField71 = 71;
            private readonly int _numField72 = 72;
            private readonly int _numField73 = 73;
            private readonly int _numField74 = 74;
            private readonly int _numField75 = 75;
            private readonly int _numField76 = 76;
            private readonly int _numField77 = 77;
            private readonly int _numField78 = 78;
            private readonly int _numField79 = 79;
            private readonly int _numField80 = 80;
            private readonly int _numField81 = 81;
            private readonly int _numField82 = 82;
            private readonly int _numField83 = 83;
            private readonly int _numField84 = 84;
            private readonly int _numField85 = 85;
            private readonly int _numField86 = 86;
            private readonly int _numField87 = 87;
            private readonly int _numField88 = 88;
            private readonly int _numField89 = 89;
            private readonly int _numField90 = 90;
            private readonly int _numField91 = 91;
            private readonly int _numField92 = 92;
            private readonly int _numField93 = 93;
            private readonly int _numField94 = 94;
            private readonly int _numField95 = 95;
            private readonly int _numField96 = 96;
            private readonly int _numField97 = 97;
            private readonly int _numField98 = 98;
            private readonly int _numField99 = 99;
            private readonly int _numField100 = 100;
            private readonly int _numField101 = 101;
            private readonly int _numField102 = 102;
            private readonly int _numField103 = 103;
            private readonly int _numField104 = 104;
            private readonly int _numField105 = 105;
            private readonly int _numField106 = 106;
            private readonly int _numField107 = 107;
            private readonly int _numField108 = 108;
            private readonly int _numField109 = 109;
            private readonly int _numField110 = 110;
            private readonly int _numField111 = 111;
            private readonly int _numField112 = 112;
            private readonly int _numField113 = 113;
            private readonly int _numField114 = 114;
            private readonly int _numField115 = 115;
            private readonly int _numField116 = 116;
            private readonly int _numField117 = 117;
            private readonly int _numField118 = 118;
            private readonly int _numField119 = 119;
            private readonly int _numField120 = 120;
            private readonly int _numField121 = 121;
            private readonly int _numField122 = 122;
            private readonly int _numField123 = 123;
            private readonly int _numField124 = 124;
            private readonly int _numField125 = 125;
            private readonly int _numField126 = 126;
            private readonly int _numField127 = 127;
            private readonly int _numField128 = 128;
            private readonly int _numField129 = 129;
            private readonly int _numField130 = 130;
            private readonly int _numField131 = 131;
            private readonly int _numField132 = 132;
            private readonly int _numField133 = 133;
            private readonly int _numField134 = 134;
            private readonly int _numField135 = 135;
            private readonly int _numField136 = 136;
            private readonly int _numField137 = 137;
            private readonly int _numField138 = 138;
            private readonly int _numField139 = 139;
            private readonly int _numField140 = 140;
            private readonly int _numField141 = 141;
            private readonly int _numField142 = 142;
            private readonly int _numField143 = 143;
            private readonly int _numField144 = 144;
            private readonly int _numField145 = 145;
            private readonly int _numField146 = 146;
            private readonly int _numField147 = 147;
            private readonly int _numField148 = 148;
            private readonly int _numField149 = 149;
            private readonly int _numField150 = 150;
            private readonly int _numField151 = 151;
            private readonly int _numField152 = 152;
            private readonly int _numField153 = 153;
            private readonly int _numField154 = 154;
            private readonly int _numField155 = 155;
            private readonly int _numField156 = 156;
            private readonly int _numField157 = 157;
            private readonly int _numField158 = 158;
            private readonly int _numField159 = 159;
            private readonly int _numField160 = 160;
            private readonly int _numField161 = 161;
            private readonly int _numField162 = 162;
            private readonly int _numField163 = 163;
            private readonly int _numField164 = 164;
            private readonly int _numField165 = 165;
            private readonly int _numField166 = 166;
            private readonly int _numField167 = 167;
            private readonly int _numField168 = 168;
            private readonly int _numField169 = 169;
            private readonly int _numField170 = 170;
            private readonly int _numField171 = 171;
            private readonly int _numField172 = 172;
            private readonly int _numField173 = 173;
            private readonly int _numField174 = 174;
            private readonly int _numField175 = 175;
            private readonly int _numField176 = 176;
            private readonly int _numField177 = 177;
            private readonly int _numField178 = 178;
            private readonly int _numField179 = 179;
            private readonly int _numField180 = 180;
            private readonly int _numField181 = 181;
            private readonly int _numField182 = 182;
            private readonly int _numField183 = 183;
            private readonly int _numField184 = 184;
            private readonly int _numField185 = 185;
            private readonly int _numField186 = 186;
            private readonly int _numField187 = 187;
            private readonly int _numField188 = 188;
            private readonly int _numField189 = 189;
            private readonly int _numField190 = 190;
            private readonly int _numField191 = 191;
            private readonly int _numField192 = 192;
            private readonly int _numField193 = 193;
            private readonly int _numField194 = 194;
            private readonly int _numField195 = 195;
            private readonly int _numField196 = 196;
            private readonly int _numField197 = 197;
            private readonly int _numField198 = 198;
            private readonly int _numField199 = 199;
            private readonly int _numField200 = 200;
            private readonly int _numField201 = 201;
            private readonly int _numField202 = 202;
            private readonly int _numField203 = 203;
            private readonly int _numField204 = 204;
            private readonly int _numField205 = 205;
            private readonly int _numField206 = 206;
            private readonly int _numField207 = 207;
            private readonly int _numField208 = 208;
            private readonly int _numField209 = 209;
            private readonly int _numField210 = 210;
            private readonly int _numField211 = 211;
            private readonly int _numField212 = 212;
            private readonly int _numField213 = 213;
            private readonly int _numField214 = 214;
            private readonly int _numField215 = 215;
            private readonly int _numField216 = 216;
            private readonly int _numField217 = 217;
            private readonly int _numField218 = 218;
            private readonly int _numField219 = 219;
            private readonly int _numField220 = 220;
            private readonly int _numField221 = 221;
            private readonly int _numField222 = 222;
            private readonly int _numField223 = 223;
            private readonly int _numField224 = 224;
            private readonly int _numField225 = 225;
            private readonly int _numField226 = 226;
            private readonly int _numField227 = 227;
            private readonly int _numField228 = 228;
            private readonly int _numField229 = 229;
            private readonly int _numField230 = 230;
            private readonly int _numField231 = 231;
            private readonly int _numField232 = 232;
            private readonly int _numField233 = 233;
            private readonly int _numField234 = 234;
            private readonly int _numField235 = 235;
            private readonly int _numField236 = 236;
            private readonly int _numField237 = 237;
            private readonly int _numField238 = 238;
            private readonly int _numField239 = 239;
            private readonly int _numField240 = 240;
            private readonly int _numField241 = 241;
            private readonly int _numField242 = 242;
            private readonly int _numField243 = 243;
            private readonly int _numField244 = 244;
            private readonly int _numField245 = 245;
            private readonly int _numField246 = 246;
            private readonly int _numField247 = 247;
            private readonly int _numField248 = 248;
            private readonly int _numField249 = 249;
            private readonly int _numField250 = 250;
            private readonly int _numField251 = 251;
            private readonly int _numField252 = 252;
            private readonly int _numField253 = 253;
            private readonly int _numField254 = 254;
            private readonly int _numField255 = 255;
            private readonly int _numField256 = 256;
            private readonly int _numField257 = 257;
            private readonly int _numField258 = 258;
            private readonly int _numField259 = 259;
            private readonly int _numField260 = 260;
            private readonly int _numField261 = 261;
            private readonly int _numField262 = 262;
            private readonly int _numField263 = 263;
            private readonly int _numField264 = 264;
            private readonly int _numField265 = 265;
            private readonly int _numField266 = 266;
            private readonly int _numField267 = 267;
            private readonly int _numField268 = 268;
            private readonly int _numField269 = 269;
            private readonly int _numField270 = 270;
            private readonly int _numField271 = 271;
            private readonly int _numField272 = 272;
            private readonly int _numField273 = 273;
            private readonly int _numField274 = 274;
            private readonly int _numField275 = 275;
            private readonly int _numField276 = 276;
            private readonly int _numField277 = 277;
            private readonly int _numField278 = 278;
            private readonly int _numField279 = 279;
            private readonly int _numField280 = 280;
            private readonly int _numField281 = 281;
            private readonly int _numField282 = 282;
            private readonly int _numField283 = 283;
            private readonly int _numField284 = 284;
            private readonly int _numField285 = 285;
            private readonly int _numField286 = 286;
            private readonly int _numField287 = 287;
            private readonly int _numField288 = 288;
            private readonly int _numField289 = 289;
            private readonly int _numField290 = 290;
            private readonly int _numField291 = 291;
            private readonly int _numField292 = 292;
            private readonly int _numField293 = 293;
            private readonly int _numField294 = 294;
            private readonly int _numField295 = 295;
            private readonly int _numField296 = 296;
            private readonly int _numField297 = 297;
            private readonly int _numField298 = 298;
            private readonly int _numField299 = 299;
            private readonly int _numField300 = 300;
            private readonly int _numField301 = 301;
            private readonly int _numField302 = 302;
            private readonly int _numField303 = 303;
            private readonly int _numField304 = 304;
            private readonly int _numField305 = 305;
            private readonly int _numField306 = 306;
            private readonly int _numField307 = 307;
            private readonly int _numField308 = 308;
            private readonly int _numField309 = 309;
            private readonly int _numField310 = 310;
            private readonly int _numField311 = 311;
            private readonly int _numField312 = 312;
            private readonly int _numField313 = 313;
            private readonly int _numField314 = 314;
            private readonly int _numField315 = 315;
            private readonly int _numField316 = 316;
            private readonly int _numField317 = 317;
            private readonly int _numField318 = 318;
            private readonly int _numField319 = 319;
            private readonly int _numField320 = 320;
            private readonly int _numField321 = 321;
            private readonly int _numField322 = 322;
            private readonly int _numField323 = 323;
            private readonly int _numField324 = 324;
            private readonly int _numField325 = 325;
            private readonly int _numField326 = 326;
            private readonly int _numField327 = 327;
            private readonly int _numField328 = 328;
            private readonly int _numField329 = 329;
            private readonly int _numField330 = 330;
            private readonly int _numField331 = 331;
            private readonly int _numField332 = 332;
            private readonly int _numField333 = 333;
            private readonly int _numField334 = 334;
            private readonly int _numField335 = 335;
            private readonly int _numField336 = 336;
            private readonly int _numField337 = 337;
            private readonly int _numField338 = 338;
            private readonly int _numField339 = 339;
            private readonly int _numField340 = 340;
            private readonly int _numField341 = 341;
            private readonly int _numField342 = 342;
            private readonly int _numField343 = 343;
            private readonly int _numField344 = 344;
            private readonly int _numField345 = 345;
            private readonly int _numField346 = 346;
            private readonly int _numField347 = 347;
            private readonly int _numField348 = 348;
            private readonly int _numField349 = 349;
            private readonly int _numField350 = 350;
            private readonly int _numField351 = 351;
            private readonly int _numField352 = 352;
            private readonly int _numField353 = 353;
            private readonly int _numField354 = 354;
            private readonly int _numField355 = 355;
            private readonly int _numField356 = 356;
            private readonly int _numField357 = 357;
            private readonly int _numField358 = 358;
            private readonly int _numField359 = 359;
            private readonly int _numField360 = 360;
            private readonly int _numField361 = 361;
            private readonly int _numField362 = 362;
            private readonly int _numField363 = 363;
            private readonly int _numField364 = 364;
            private readonly int _numField365 = 365;
            private readonly int _numField366 = 366;
            private readonly int _numField367 = 367;
            private readonly int _numField368 = 368;
            private readonly int _numField369 = 369;
            private readonly int _numField370 = 370;
            private readonly int _numField371 = 371;
            private readonly int _numField372 = 372;
            private readonly int _numField373 = 373;
            private readonly int _numField374 = 374;
            private readonly int _numField375 = 375;
            private readonly int _numField376 = 376;
            private readonly int _numField377 = 377;
            private readonly int _numField378 = 378;
            private readonly int _numField379 = 379;
            private readonly int _numField380 = 380;
            private readonly int _numField381 = 381;
            private readonly int _numField382 = 382;
            private readonly int _numField383 = 383;
            private readonly int _numField384 = 384;
            private readonly int _numField385 = 385;
            private readonly int _numField386 = 386;
            private readonly int _numField387 = 387;
            private readonly int _numField388 = 388;
            private readonly int _numField389 = 389;
            private readonly int _numField390 = 390;
            private readonly int _numField391 = 391;
            private readonly int _numField392 = 392;
            private readonly int _numField393 = 393;
            private readonly int _numField394 = 394;
            private readonly int _numField395 = 395;
            private readonly int _numField396 = 396;
            private readonly int _numField397 = 397;
            private readonly int _numField398 = 398;
            private readonly int _numField399 = 399;
            private readonly int _numField400 = 400;
            private readonly int _numField401 = 401;
            private readonly int _numField402 = 402;
            private readonly int _numField403 = 403;
            private readonly int _numField404 = 404;
            private readonly int _numField405 = 405;
            private readonly int _numField406 = 406;
            private readonly int _numField407 = 407;
            private readonly int _numField408 = 408;
            private readonly int _numField409 = 409;
            private readonly int _numField410 = 410;
            private readonly int _numField411 = 411;
            private readonly int _numField412 = 412;
            private readonly int _numField413 = 413;
            private readonly int _numField414 = 414;
            private readonly int _numField415 = 415;
            private readonly int _numField416 = 416;
            private readonly int _numField417 = 417;
            private readonly int _numField418 = 418;
            private readonly int _numField419 = 419;
            private readonly int _numField420 = 420;
            private readonly int _numField421 = 421;
            private readonly int _numField422 = 422;
            private readonly int _numField423 = 423;
            private readonly int _numField424 = 424;
            private readonly int _numField425 = 425;
            private readonly int _numField426 = 426;
            private readonly int _numField427 = 427;
            private readonly int _numField428 = 428;
            private readonly int _numField429 = 429;
            private readonly int _numField430 = 430;
            private readonly int _numField431 = 431;
            private readonly int _numField432 = 432;
            private readonly int _numField433 = 433;
            private readonly int _numField434 = 434;
            private readonly int _numField435 = 435;
            private readonly int _numField436 = 436;
            private readonly int _numField437 = 437;
            private readonly int _numField438 = 438;
            private readonly int _numField439 = 439;
            private readonly int _numField440 = 440;
            private readonly int _numField441 = 441;
            private readonly int _numField442 = 442;
            private readonly int _numField443 = 443;
            private readonly int _numField444 = 444;
            private readonly int _numField445 = 445;
            private readonly int _numField446 = 446;
            private readonly int _numField447 = 447;
            private readonly int _numField448 = 448;
            private readonly int _numField449 = 449;
            private readonly int _numField450 = 450;
            private readonly int _numField451 = 451;
            private readonly int _numField452 = 452;
            private readonly int _numField453 = 453;
            private readonly int _numField454 = 454;
            private readonly int _numField455 = 455;
            private readonly int _numField456 = 456;
            private readonly int _numField457 = 457;
            private readonly int _numField458 = 458;
            private readonly int _numField459 = 459;
            private readonly int _numField460 = 460;
            private readonly int _numField461 = 461;
            private readonly int _numField462 = 462;
            private readonly int _numField463 = 463;
            private readonly int _numField464 = 464;
            private readonly int _numField465 = 465;
            private readonly int _numField466 = 466;
            private readonly int _numField467 = 467;
            private readonly int _numField468 = 468;
            private readonly int _numField469 = 469;
            private readonly int _numField470 = 470;
            private readonly int _numField471 = 471;
            private readonly int _numField472 = 472;
            private readonly int _numField473 = 473;
            private readonly int _numField474 = 474;
            private readonly int _numField475 = 475;
            private readonly int _numField476 = 476;
            private readonly int _numField477 = 477;
            private readonly int _numField478 = 478;
            private readonly int _numField479 = 479;
            private readonly int _numField480 = 480;
            private readonly int _numField481 = 481;
            private readonly int _numField482 = 482;
            private readonly int _numField483 = 483;
            private readonly int _numField484 = 484;
            private readonly int _numField485 = 485;
            private readonly int _numField486 = 486;
            private readonly int _numField487 = 487;
            private readonly int _numField488 = 488;
            private readonly int _numField489 = 489;
            private readonly int _numField490 = 490;
            private readonly int _numField491 = 491;
            private readonly int _numField492 = 492;
            private readonly int _numField493 = 493;
            private readonly int _numField494 = 494;
            private readonly int _numField495 = 495;
            private readonly int _numField496 = 496;
            private readonly int _numField497 = 497;
            private readonly int _numField498 = 498;
            private readonly int _numField499 = 499;
            private readonly int _numField500 = 500;
            private readonly int _numField501 = 501;
            private readonly int _numField502 = 502;
            private readonly int _numField503 = 503;
            private readonly int _numField504 = 504;
            private readonly int _numField505 = 505;
            private readonly int _numField506 = 506;
            private readonly int _numField507 = 507;
            private readonly int _numField508 = 508;
            private readonly int _numField509 = 509;
            private readonly int _numField510 = 510;
            private readonly int _numField511 = 511;
            private readonly int _numField512 = 512;
            private readonly int _numField513 = 513;
            private readonly int _numField514 = 514;
            private readonly int _numField515 = 515;
            private readonly int _numField516 = 516;
            private readonly int _numField517 = 517;
            private readonly int _numField518 = 518;
            private readonly int _numField519 = 519;
            private readonly int _numField520 = 520;
            private readonly int _numField521 = 521;
            private readonly int _numField522 = 522;
            private readonly int _numField523 = 523;
            private readonly int _numField524 = 524;
            private readonly int _numField525 = 525;
            private readonly int _numField526 = 526;
            private readonly int _numField527 = 527;
            private readonly int _numField528 = 528;
            private readonly int _numField529 = 529;
            private readonly int _numField530 = 530;
            private readonly int _numField531 = 531;
            private readonly int _numField532 = 532;
            private readonly int _numField533 = 533;
            private readonly int _numField534 = 534;
            private readonly int _numField535 = 535;
            private readonly int _numField536 = 536;
            private readonly int _numField537 = 537;
            private readonly int _numField538 = 538;
            private readonly int _numField539 = 539;
            private readonly int _numField540 = 540;
            private readonly int _numField541 = 541;
            private readonly int _numField542 = 542;
            private readonly int _numField543 = 543;
            private readonly int _numField544 = 544;
            private readonly int _numField545 = 545;
            private readonly int _numField546 = 546;
            private readonly int _numField547 = 547;
            private readonly int _numField548 = 548;
            private readonly int _numField549 = 549;
            private readonly int _numField550 = 550;
            private readonly int _numField551 = 551;
            private readonly int _numField552 = 552;
            private readonly int _numField553 = 553;
            private readonly int _numField554 = 554;
            private readonly int _numField555 = 555;
            private readonly int _numField556 = 556;
            private readonly int _numField557 = 557;
            private readonly int _numField558 = 558;
            private readonly int _numField559 = 559;
            private readonly int _numField560 = 560;
            private readonly int _numField561 = 561;
            private readonly int _numField562 = 562;
            private readonly int _numField563 = 563;
            private readonly int _numField564 = 564;
            private readonly int _numField565 = 565;
            private readonly int _numField566 = 566;
            private readonly int _numField567 = 567;
            private readonly int _numField568 = 568;
            private readonly int _numField569 = 569;
            private readonly int _numField570 = 570;
            private readonly int _numField571 = 571;
            private readonly int _numField572 = 572;
            private readonly int _numField573 = 573;
            private readonly int _numField574 = 574;
            private readonly int _numField575 = 575;
            private readonly int _numField576 = 576;
            private readonly int _numField577 = 577;
            private readonly int _numField578 = 578;
            private readonly int _numField579 = 579;
            private readonly int _numField580 = 580;
            private readonly int _numField581 = 581;
            private readonly int _numField582 = 582;
            private readonly int _numField583 = 583;
            private readonly int _numField584 = 584;
            private readonly int _numField585 = 585;
            private readonly int _numField586 = 586;
            private readonly int _numField587 = 587;
            private readonly int _numField588 = 588;
            private readonly int _numField589 = 589;
            private readonly int _numField590 = 590;
            private readonly int _numField591 = 591;
            private readonly int _numField592 = 592;
            private readonly int _numField593 = 593;
            private readonly int _numField594 = 594;
            private readonly int _numField595 = 595;
            private readonly int _numField596 = 596;
            private readonly int _numField597 = 597;
            private readonly int _numField598 = 598;
            private readonly int _numField599 = 599;
            private readonly int _numField600 = 600;
            private readonly int _numField601 = 601;
            private readonly int _numField602 = 602;
            private readonly int _numField603 = 603;
            private readonly int _numField604 = 604;
            private readonly int _numField605 = 605;
            private readonly int _numField606 = 606;
            private readonly int _numField607 = 607;
            private readonly int _numField608 = 608;
            private readonly int _numField609 = 609;
            private readonly int _numField610 = 610;
            private readonly int _numField611 = 611;
            private readonly int _numField612 = 612;
            private readonly int _numField613 = 613;
            private readonly int _numField614 = 614;
            private readonly int _numField615 = 615;
            private readonly int _numField616 = 616;
            private readonly int _numField617 = 617;
            private readonly int _numField618 = 618;
            private readonly int _numField619 = 619;
            private readonly int _numField620 = 620;
            private readonly int _numField621 = 621;
            private readonly int _numField622 = 622;
            private readonly int _numField623 = 623;
            private readonly int _numField624 = 624;
            private readonly int _numField625 = 625;
            private readonly int _numField626 = 626;
            private readonly int _numField627 = 627;
            private readonly int _numField628 = 628;
            private readonly int _numField629 = 629;
            private readonly int _numField630 = 630;
            private readonly int _numField631 = 631;
            private readonly int _numField632 = 632;
            private readonly int _numField633 = 633;
            private readonly int _numField634 = 634;
            private readonly int _numField635 = 635;
            private readonly int _numField636 = 636;
            private readonly int _numField637 = 637;
            private readonly int _numField638 = 638;
            private readonly int _numField639 = 639;
            private readonly int _numField640 = 640;
            private readonly int _numField641 = 641;
            private readonly int _numField642 = 642;
            private readonly int _numField643 = 643;
            private readonly int _numField644 = 644;
            private readonly int _numField645 = 645;
            private readonly int _numField646 = 646;
            private readonly int _numField647 = 647;
            private readonly int _numField648 = 648;
            private readonly int _numField649 = 649;
            private readonly int _numField650 = 650;
            private readonly int _numField651 = 651;
            private readonly int _numField652 = 652;
            private readonly int _numField653 = 653;
            private readonly int _numField654 = 654;
            private readonly int _numField655 = 655;
            private readonly int _numField656 = 656;
            private readonly int _numField657 = 657;
            private readonly int _numField658 = 658;
            private readonly int _numField659 = 659;
            private readonly int _numField660 = 660;
            private readonly int _numField661 = 661;
            private readonly int _numField662 = 662;
            private readonly int _numField663 = 663;
            private readonly int _numField664 = 664;
            private readonly int _numField665 = 665;
            private readonly int _numField666 = 666;
            private readonly int _numField667 = 667;
            private readonly int _numField668 = 668;
            private readonly int _numField669 = 669;
            private readonly int _numField670 = 670;
            private readonly int _numField671 = 671;
            private readonly int _numField672 = 672;
            private readonly int _numField673 = 673;
            private readonly int _numField674 = 674;
            private readonly int _numField675 = 675;
            private readonly int _numField676 = 676;
            private readonly int _numField677 = 677;
            private readonly int _numField678 = 678;
            private readonly int _numField679 = 679;
            private readonly int _numField680 = 680;
            private readonly int _numField681 = 681;
            private readonly int _numField682 = 682;
            private readonly int _numField683 = 683;
            private readonly int _numField684 = 684;
            private readonly int _numField685 = 685;
            private readonly int _numField686 = 686;
            private readonly int _numField687 = 687;
            private readonly int _numField688 = 688;
            private readonly int _numField689 = 689;
            private readonly int _numField690 = 690;
            private readonly int _numField691 = 691;
            private readonly int _numField692 = 692;
            private readonly int _numField693 = 693;
            private readonly int _numField694 = 694;
            private readonly int _numField695 = 695;
            private readonly int _numField696 = 696;
            private readonly int _numField697 = 697;
            private readonly int _numField698 = 698;
            private readonly int _numField699 = 699;
            private readonly int _numField700 = 700;
            private readonly int _numField701 = 701;
            private readonly int _numField702 = 702;
            private readonly int _numField703 = 703;
            private readonly int _numField704 = 704;
            private readonly int _numField705 = 705;
            private readonly int _numField706 = 706;
            private readonly int _numField707 = 707;
            private readonly int _numField708 = 708;
            private readonly int _numField709 = 709;
            private readonly int _numField710 = 710;
            private readonly int _numField711 = 711;
            private readonly int _numField712 = 712;
            private readonly int _numField713 = 713;
            private readonly int _numField714 = 714;
            private readonly int _numField715 = 715;
            private readonly int _numField716 = 716;
            private readonly int _numField717 = 717;
            private readonly int _numField718 = 718;
            private readonly int _numField719 = 719;
            private readonly int _numField720 = 720;
            private readonly int _numField721 = 721;
            private readonly int _numField722 = 722;
            private readonly int _numField723 = 723;
            private readonly int _numField724 = 724;
            private readonly int _numField725 = 725;
            private readonly int _numField726 = 726;
            private readonly int _numField727 = 727;
            private readonly int _numField728 = 728;
            private readonly int _numField729 = 729;
            private readonly int _numField730 = 730;
            private readonly int _numField731 = 731;
            private readonly int _numField732 = 732;
            private readonly int _numField733 = 733;
            private readonly int _numField734 = 734;
            private readonly int _numField735 = 735;
            private readonly int _numField736 = 736;
            private readonly int _numField737 = 737;
            private readonly int _numField738 = 738;
            private readonly int _numField739 = 739;
            private readonly int _numField740 = 740;
            private readonly int _numField741 = 741;
            private readonly int _numField742 = 742;
            private readonly int _numField743 = 743;
            private readonly int _numField744 = 744;
            private readonly int _numField745 = 745;
            private readonly int _numField746 = 746;
            private readonly int _numField747 = 747;
            private readonly int _numField748 = 748;
            private readonly int _numField749 = 749;
            private readonly int _numField750 = 750;
            private readonly int _numField751 = 751;
            private readonly int _numField752 = 752;
            private readonly int _numField753 = 753;
            private readonly int _numField754 = 754;
            private readonly int _numField755 = 755;
            private readonly int _numField756 = 756;
            private readonly int _numField757 = 757;
            private readonly int _numField758 = 758;
            private readonly int _numField759 = 759;
            private readonly int _numField760 = 760;
            private readonly int _numField761 = 761;
            private readonly int _numField762 = 762;
            private readonly int _numField763 = 763;
            private readonly int _numField764 = 764;
            private readonly int _numField765 = 765;
            private readonly int _numField766 = 766;
            private readonly int _numField767 = 767;
            private readonly int _numField768 = 768;
            private readonly int _numField769 = 769;
            private readonly int _numField770 = 770;
            private readonly int _numField771 = 771;
            private readonly int _numField772 = 772;
            private readonly int _numField773 = 773;
            private readonly int _numField774 = 774;
            private readonly int _numField775 = 775;
            private readonly int _numField776 = 776;
            private readonly int _numField777 = 777;
            private readonly int _numField778 = 778;
            private readonly int _numField779 = 779;
            private readonly int _numField780 = 780;
            private readonly int _numField781 = 781;
            private readonly int _numField782 = 782;
            private readonly int _numField783 = 783;
            private readonly int _numField784 = 784;
            private readonly int _numField785 = 785;
            private readonly int _numField786 = 786;
            private readonly int _numField787 = 787;
            private readonly int _numField788 = 788;
            private readonly int _numField789 = 789;
            private readonly int _numField790 = 790;
            private readonly int _numField791 = 791;
            private readonly int _numField792 = 792;
            private readonly int _numField793 = 793;
            private readonly int _numField794 = 794;
            private readonly int _numField795 = 795;
            private readonly int _numField796 = 796;
            private readonly int _numField797 = 797;
            private readonly int _numField798 = 798;
            private readonly int _numField799 = 799;
            private readonly int _numField800 = 800;
            private readonly int _numField801 = 801;
            private readonly int _numField802 = 802;
            private readonly int _numField803 = 803;
            private readonly int _numField804 = 804;
            private readonly int _numField805 = 805;
            private readonly int _numField806 = 806;
            private readonly int _numField807 = 807;
            private readonly int _numField808 = 808;
            private readonly int _numField809 = 809;
            private readonly int _numField810 = 810;
            private readonly int _numField811 = 811;
            private readonly int _numField812 = 812;
            private readonly int _numField813 = 813;
            private readonly int _numField814 = 814;
            private readonly int _numField815 = 815;
            private readonly int _numField816 = 816;
            private readonly int _numField817 = 817;
            private readonly int _numField818 = 818;
            private readonly int _numField819 = 819;
            private readonly int _numField820 = 820;
            private readonly int _numField821 = 821;
            private readonly int _numField822 = 822;
            private readonly int _numField823 = 823;
            private readonly int _numField824 = 824;
            private readonly int _numField825 = 825;
            private readonly int _numField826 = 826;
            private readonly int _numField827 = 827;
            private readonly int _numField828 = 828;
            private readonly int _numField829 = 829;
            private readonly int _numField830 = 830;
            private readonly int _numField831 = 831;
            private readonly int _numField832 = 832;
            private readonly int _numField833 = 833;
            private readonly int _numField834 = 834;
            private readonly int _numField835 = 835;
            private readonly int _numField836 = 836;
            private readonly int _numField837 = 837;
            private readonly int _numField838 = 838;
            private readonly int _numField839 = 839;
            private readonly int _numField840 = 840;
            private readonly int _numField841 = 841;
            private readonly int _numField842 = 842;
            private readonly int _numField843 = 843;
            private readonly int _numField844 = 844;
            private readonly int _numField845 = 845;
            private readonly int _numField846 = 846;
            private readonly int _numField847 = 847;
            private readonly int _numField848 = 848;
            private readonly int _numField849 = 849;
            private readonly int _numField850 = 850;
            private readonly int _numField851 = 851;
            private readonly int _numField852 = 852;
            private readonly int _numField853 = 853;
            private readonly int _numField854 = 854;
            private readonly int _numField855 = 855;
            private readonly int _numField856 = 856;
            private readonly int _numField857 = 857;
            private readonly int _numField858 = 858;
            private readonly int _numField859 = 859;
            private readonly int _numField860 = 860;
            private readonly int _numField861 = 861;
            private readonly int _numField862 = 862;
            private readonly int _numField863 = 863;
            private readonly int _numField864 = 864;
            private readonly int _numField865 = 865;
            private readonly int _numField866 = 866;
            private readonly int _numField867 = 867;
            private readonly int _numField868 = 868;
            private readonly int _numField869 = 869;
            private readonly int _numField870 = 870;
            private readonly int _numField871 = 871;
            private readonly int _numField872 = 872;
            private readonly int _numField873 = 873;
            private readonly int _numField874 = 874;
            private readonly int _numField875 = 875;
            private readonly int _numField876 = 876;
            private readonly int _numField877 = 877;
            private readonly int _numField878 = 878;
            private readonly int _numField879 = 879;
            private readonly int _numField880 = 880;
            private readonly int _numField881 = 881;
            private readonly int _numField882 = 882;
            private readonly int _numField883 = 883;
            private readonly int _numField884 = 884;
            private readonly int _numField885 = 885;
            private readonly int _numField886 = 886;
            private readonly int _numField887 = 887;
            private readonly int _numField888 = 888;
            private readonly int _numField889 = 889;
            private readonly int _numField890 = 890;
            private readonly int _numField891 = 891;
            private readonly int _numField892 = 892;
            private readonly int _numField893 = 893;
            private readonly int _numField894 = 894;
            private readonly int _numField895 = 895;
            private readonly int _numField896 = 896;
            private readonly int _numField897 = 897;
            private readonly int _numField898 = 898;
            private readonly int _numField899 = 899;
            private readonly int _numField900 = 900;
            private readonly int _numField901 = 901;
            private readonly int _numField902 = 902;
            private readonly int _numField903 = 903;
            private readonly int _numField904 = 904;
            private readonly int _numField905 = 905;
            private readonly int _numField906 = 906;
            private readonly int _numField907 = 907;
            private readonly int _numField908 = 908;
            private readonly int _numField909 = 909;
            private readonly int _numField910 = 910;
            private readonly int _numField911 = 911;
            private readonly int _numField912 = 912;
            private readonly int _numField913 = 913;
            private readonly int _numField914 = 914;
            private readonly int _numField915 = 915;
            private readonly int _numField916 = 916;
            private readonly int _numField917 = 917;
            private readonly int _numField918 = 918;
            private readonly int _numField919 = 919;
            private readonly int _numField920 = 920;
            private readonly int _numField921 = 921;
            private readonly int _numField922 = 922;
            private readonly int _numField923 = 923;
            private readonly int _numField924 = 924;
            private readonly int _numField925 = 925;
            private readonly int _numField926 = 926;
            private readonly int _numField927 = 927;
            private readonly int _numField928 = 928;
            private readonly int _numField929 = 929;
            private readonly int _numField930 = 930;
            private readonly int _numField931 = 931;
            private readonly int _numField932 = 932;
            private readonly int _numField933 = 933;
            private readonly int _numField934 = 934;
            private readonly int _numField935 = 935;
            private readonly int _numField936 = 936;
            private readonly int _numField937 = 937;
            private readonly int _numField938 = 938;
            private readonly int _numField939 = 939;
            private readonly int _numField940 = 940;
            private readonly int _numField941 = 941;
            private readonly int _numField942 = 942;
            private readonly int _numField943 = 943;
            private readonly int _numField944 = 944;
            private readonly int _numField945 = 945;
            private readonly int _numField946 = 946;
            private readonly int _numField947 = 947;
            private readonly int _numField948 = 948;
            private readonly int _numField949 = 949;
            private readonly int _numField950 = 950;
            private readonly int _numField951 = 951;
            private readonly int _numField952 = 952;
            private readonly int _numField953 = 953;
            private readonly int _numField954 = 954;
            private readonly int _numField955 = 955;
            private readonly int _numField956 = 956;
            private readonly int _numField957 = 957;
            private readonly int _numField958 = 958;
            private readonly int _numField959 = 959;
            private readonly int _numField960 = 960;
            private readonly int _numField961 = 961;
            private readonly int _numField962 = 962;
            private readonly int _numField963 = 963;
            private readonly int _numField964 = 964;
            private readonly int _numField965 = 965;
            private readonly int _numField966 = 966;
            private readonly int _numField967 = 967;
            private readonly int _numField968 = 968;
            private readonly int _numField969 = 969;
            private readonly int _numField970 = 970;
            private readonly int _numField971 = 971;
            private readonly int _numField972 = 972;
            private readonly int _numField973 = 973;
            private readonly int _numField974 = 974;
            private readonly int _numField975 = 975;
            private readonly int _numField976 = 976;
            private readonly int _numField977 = 977;
            private readonly int _numField978 = 978;
            private readonly int _numField979 = 979;
            private readonly int _numField980 = 980;
            private readonly int _numField981 = 981;
            private readonly int _numField982 = 982;
            private readonly int _numField983 = 983;
            private readonly int _numField984 = 984;
            private readonly int _numField985 = 985;
            private readonly int _numField986 = 986;
            private readonly int _numField987 = 987;
            private readonly int _numField988 = 988;
            private readonly int _numField989 = 989;
            private readonly int _numField990 = 990;
            private readonly int _numField991 = 991;
            private readonly int _numField992 = 992;
            private readonly int _numField993 = 993;
            private readonly int _numField994 = 994;
            private readonly int _numField995 = 995;
            private readonly int _numField996 = 996;
            private readonly int _numField997 = 997;
            private readonly int _numField998 = 998;
            private readonly int _numField999 = 999;
            private readonly int _numField1000 = 1000;
        }
    }
}
