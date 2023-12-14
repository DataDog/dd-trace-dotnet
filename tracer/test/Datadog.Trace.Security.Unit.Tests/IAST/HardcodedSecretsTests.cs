// <copyright file="HardcodedSecretsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Analyzers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST;

public class HardcodedSecretsTests : IClassFixture<HardcodedSecretsTests.HardcodedSecretsFixture>
{
    private readonly HardcodedSecretsAnalyzer _analyzer;

    public HardcodedSecretsTests(HardcodedSecretsFixture fixture)
    {
        _analyzer = fixture.Analyzer;
    }

    [Theory]
    [InlineData("private-key", "-----BEGIN OPENSSH PRIVATE KEY-----\r\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\r\nQyNTUxOQAAACA8YWKYztuuvxUIMomc3zv0OdXCT57Cc2cRYu3TMbX9XAAAAJDiKO3C4ijt\r\nwgAAAAtzc2gtZWQyNTUxOQAAACA8YWKYztuuvxUIMomc3zv0OdXCT57Cc2cRYu3TMbX9XA\r\nAAAECzmj8DGxg5YHtBK4AmBttMXDQHsPAaCyYHQjJ4YujRBTxhYpjO266/FQgyiZzfO/Q5\r\n1cJPnsJzZxFi7dMxtf1cAAAADHJvb3RAZGV2aG9zdAE=\r\n-----END OPENSSH PRIVATE KEY-----\r\n")]
    [InlineData("aws-access-token", @"A3T43DROGX[[DD_SECRET]]R2PGF4BI5T")]
    [InlineData("aws-access-token", @"AKIAMET3GG[[DD_SECRET]]RVNYJ34BED")]
    [InlineData("aws-access-token", @"AGPACLHLO1[[DD_SECRET]]K7M23EG1XF")]
    [InlineData("aws-access-token", @"AIDAYRK0X7[[DD_SECRET]]A328I8UEA1")]
    [InlineData("aws-access-token", @"AROAGTMJ7N[[DD_SECRET]]J63NYRH7VY")]
    [InlineData("aws-access-token", @"AIPAMX2ROO[[DD_SECRET]]GRW3B81X2M")]
    [InlineData("aws-access-token", @"ANPAVE250E[[DD_SECRET]]B33TMMVAWJ")]
    [InlineData("aws-access-token", @"ANVA1N8L1F[[DD_SECRET]]F1LUH7CZ5E")]
    [InlineData("aws-access-token", @"ASIALR53WS[[DD_SECRET]]0XV44FA2PZ")]
    [InlineData("private-key", @"-----BEGIN8QOC5_YEQV5825RI30FPCF6IZFLMXL33JG5Q13STI0H9836AO9IDKL[[DD_SECRET]]52VGO0YMTE4BYWABTAHXGN_8L8LL90CZT_MCPRIVATE KEY------ ---KEY----")]
    [InlineData("adobe-client-secret", @"p8e-B7eFd4141F7c59[[DD_SECRET]]90E81A3777d82FA0A1")]
    [InlineData("age-secret-key", @"AGE-SECRET-KEY-1QQQQQQQQQQQQQQQQQQQQQ[[DD_SECRET]]QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ")]
    [InlineData("alibaba-access-key-id", @"LTAI54k65pzn[[DD_SECRET]]b1thmy5oy8i1")]
    [InlineData("authress-service-client-access-key", @"sc_j0351.nakp.acc_jj--uomkgar144yxuwnii4sxdrbe.op4n4[[DD_SECRET]]_d298lz=ya6--bet7p/ixs5o+doj-7z3qc+x_15dykju8etaa7va")]
    [InlineData("authress-service-client-access-key", @"ext_egrlm917o91ncu7fttfv4vlhks.vmhfkw.acc-k6ojvzaeikqe1tu5rkno-t6ka4hfwg.t5imc4l_sx3qb5h5[[DD_SECRET]]kct49/i+5-fj5u9wbs-owjtehn/7lf8u8c_eer=wwmvczbh=3hox58jbxpwv-qz0gzp2w4rzus6j3qufg=dd6l4a7")]
    [InlineData("authress-service-client-access-key", @"scauth_t05ptey579xro900dlb9lf5pnblt.55n5wh.acc_2c7yv4ssrz[[DD_SECRET]]udmcgp-5wn6qbs4.abely65c=aqoru1o_2tih62spbb87maq8r7spid5ol")]
    [InlineData("authress-service-client-access-key", @"authress_gawpezs.mav7h.acc-y7u17jx-at-yh-1w0./3vs4z/gm61pjtye3lvarbsh-errtv6[[DD_SECRET]]4m4bu7ig_85_8n0lucdkse2r+kos0ht15a/cusemvfxh1dy8o7_opln0zmfjs4u5odh7qx5b=63mv")]
    [InlineData("clojars-api-token", @"CLOJARS_13xsd3iejrykpgl542w24u61p2[[DD_SECRET]]b6ovubdeulu8xysi3zfefu0d87y0xg66cl")]
    [InlineData("databricks-api-token", @"dapig5483df39f7c9e[[DD_SECRET]]25d3f30df33gg0gh47")]
    [InlineData("digitalocean-pat", @"dop_v1_8c2c6187957b651a05e54ec1aee7[[DD_SECRET]]7d33a7abb30b0620137480950c439ab1db95")]
    [InlineData("digitalocean-access-token", @"doo_v1_da95f96ecf766911480e8d7d0ba2[[DD_SECRET]]98e77ed27921c0e667db4a2d20ead6ee8efd")]
    [InlineData("digitalocean-refresh-token", @"dor_v1_a91584509b18a037978fbe09dfa4[[DD_SECRET]]e38aa5586f50763f6bcf4081079621233abf")]
    [InlineData("doppler-api-token", @"dp.pt.p6lzzdxeutkydgfgza[[DD_SECRET]]5ypwf38mj2d32zhdng3x8kil4")]
    [InlineData("duffel-api-token", @"duffel_test_zcrmor7ai0443t2[[DD_SECRET]]o77a33ws=5-nbcxrd46g88_q0_4t")]
    [InlineData("duffel-api-token", @"duffel_live_mwqm_xqomewcxsu[[DD_SECRET]]pd6-xi0a9n7s9qn5=c_0ivaesgd4")]
    [InlineData("dynatrace-api-token", @"dt0c01.jdkpoaud36kwur9ces79kvax.l4c0rqf6mxjubsp1[[DD_SECRET]]zggwbxcbbmxf2nydwxdgrnlqfzh3zscashvp3b0x2azf2ad9")]
    [InlineData("easypost-api-token", @"EZAKf8ooijl40rmpilsjwwv1ijv6p[[DD_SECRET]]nrygxfvh4utywj7kudtoitzg5gnsi")]
    [InlineData("flutterwave-public-key", @"FLWPUBK_TEST-8g097634bf[[DD_SECRET]]5dh592ag304ca1d751f05b-X")]
    [InlineData("frameio-api-token", @"fio-u-dzhhzqf09aizag-hlgb74c2m_3aad[[DD_SECRET]]oxvzile4pn-u43f58h07mam2iha98i9pewv")]
    [InlineData("gcp-api-key", @"AIzah_ptpRup6L0JcW6[[DD_SECRET]]aLBnMJpR1HCvCSYQCEk8")]
    [InlineData("github-pat", @"ghp_nO2T8vDTcIjm6k3W[[DD_SECRET]]Nb7RyLABwGlzVN5Sndbw")]
    [InlineData("github-fine-grained-pat", @"github_pat_TdNaxQzTx57ttp2CgkCeqns94YoDr_T9yOV[[DD_SECRET]]YkI1X1J_Bf6so3g7UzhIvME6zrXsycYWFu4iziGdIoSVYv_")]
    [InlineData("github-oauth", @"gho_bRZAg6pbGAsv5hqn[[DD_SECRET]]haNUZYyBT406UI83rngG")]
    [InlineData("github-app-token", @"ghu_mFy4SBXQa7qXtDNH[[DD_SECRET]]5AdjnHb5ihXso3I0KaUv")]
    [InlineData("github-app-token", @"ghs_P6mALIXA9r5lmmM3[[DD_SECRET]]mQFtsMADvr7ppdjIhsgt")]
    [InlineData("gitlab-pat", @"glpat-KC-ew9R[[DD_SECRET]]U72IXZokf1O3w")]
    [InlineData("gitlab-ptt", @"glptt-15982f250e143a3b4[[DD_SECRET]]f9cc66677df74a3f1a46a53")]
    [InlineData("gitlab-rrt", @"GR1348941w48CD[[DD_SECRET]]ML5zNRrM-IWc4K6")]
    [InlineData("grafana-api-key", @"eyJrIjoi3wN3VFA6TjlZDPTRy1rF34e0vNI6XLlmGimR4sQotIPLOBrtYnXMBAP[[DD_SECRET]]8RXHI7EjH89SemBWFlCWvpSlufZityYkFTo9gkIroiINOsNGWQRl9oeS8vXmigz")]
    [InlineData("grafana-cloud-api-token", @"glc_ZNL+B2gdVtMiFXBSSMehylc1WBwNgJJu0p1gCJVayJq8Sw5ZXVQVWyy0xWnAGT9MketcigPJ2Dvp7gilHW2K40XC3BvshUUeMeYzDd22FHeSCI4QzC5vdyTw[[DD_SECRET]]GrkhwjoivQddHviqf/ExVE/uIe5eblBZKotX4VFie7jyraJEgPPdtiaBs2YSqJHJODTfCKxK5iSagMCtV1+LVjsoOrrC9z3VzajoHpCVdFkKP/gfzq81K70rXqR=")]
    [InlineData("grafana-service-account-token", @"glsa_MGp6lGoZkqxosP9gYN[[DD_SECRET]]KLOR0CL2k2QcWq_47efacc6")]
    [InlineData("hashicorp-tf-api-token", @"cjbshuy4jbgkr6.atlasv1.y42u3ffme5=nuad63n7[[DD_SECRET]]ixa00ahqhmtvj_i8uz8cj--91iif6hiq-y1zydctw2_")]
    [InlineData("jwt", @"eyMpUxtiJl2rMrOf7CX.eyL-geNN[[DD_SECRET]]sKqBMyGF_8t.D21TNcJQaA-JiN==")]
    [InlineData("jwt", @"ey4HNigV2jJFWitTWG97o2.e[[DD_SECRET]]yeI9N4KUt-Q-vbaX0uVe1ED.")]
    [InlineData("linear-api-key", @"lin_api_55dj1wv1kku4azti[[DD_SECRET]]hf4v423kzazyhfb7vrc01k8y")]
    [InlineData("npm-access-token", @"npm_6zbqg0d1s4xv5hdc[[DD_SECRET]]3t067teixitw3q6hgg7d")]
    [InlineData("openai-api-key", @"sk-ZIrL3SsytTr8P6UnVCwKT3[[DD_SECRET]]BlbkFJY5mE7c6CAQqar89BsbYY")]
    [InlineData("planetscale-password", @"pscale_pw_nujzfug.q3xu.ayde-1o-9cb[[DD_SECRET]]_p_45srv4-tr_76u5tb_1um_pohdfs=rri1")]
    [InlineData("planetscale-api-token", @"pscale_tkn_vqh-lb3bvhelegws7ensdap[[DD_SECRET]]qlnqmbb.4gc_pld0z4kqhj=__9sxn8i1vb")]
    [InlineData("planetscale-oauth-token", @"pscale_oauth_0-yr.uioechhh4r126xhgo[[DD_SECRET]]prqxrz9-orour2bnl=_95=52k79gu9-7xlw")]
    [InlineData("postman-api-token", @"PMAK-a0b31c5574f54f25de3145f3-51[[DD_SECRET]]1228cc7e87013da732c1cfd3f5b2eb17")]
    [InlineData("prefect-api-token", @"pnu_0iil1hu3cx5348kw[[DD_SECRET]]4zcuilej5vocla2mtz68")]
    [InlineData("pulumi-api-token", @"pul-27f34b5f61678100e1[[DD_SECRET]]0b91c622a72ebbf7e7693d")]
    [InlineData("pypi-upload-token", @"pypi-AgEIcHlwaS5vcmc5FGoPciZ7RPe20Gsi[[DD_SECRET]]FKA11hXtfCJhqT5LsEWDJRDgdm_4ldBNKO4aP")]
    [InlineData("readme-api-token", @"rdme_cu070oo5j8mkf10wn3drnitreba7nppr[[DD_SECRET]]7ea2lhsxevlbtkljqs4cl0h3aj3zoqs4t4nb08")]
    [InlineData("rubygems-api-token", @"rubygems_ce1f6819755173fcd8c[[DD_SECRET]]6a06e4b1248e22d467f082e99494d")]
    [InlineData("scalingo-api-token", @"tk-us-RkgJkrWFnTTA_r5Cn8P7K[[DD_SECRET]]v1HrqWA0MSBquaWl-w75sOQRGHc")]
    [InlineData("sendgrid-api-token", @"SG.vrbamynnwl=uo1yalg1kfkjy2al1g74[[DD_SECRET]]22mkg0q_20ry1rqwyb0po75-lirllmzg2vh")]
    [InlineData("sendinblue-api-token", @"xkeysib-a01f7d93542ce14421078f99dc40ae8cd32d[[DD_SECRET]]3e2c8de44e97494ecf7402b52321-o7y8rdh7i90dmk9e")]
    [InlineData("shippo-api-token", @"shippo_live_5a33a8576ad63e[[DD_SECRET]]477dfe7c70f8fc3bdc35717c0a")]
    [InlineData("shippo-api-token", @"shippo_test_c5aac45be1a479[[DD_SECRET]]3cceb33d47bc81c957ef5170ce")]
    [InlineData("shopify-shared-secret", @"shpss_aA1dE53AA26AD[[DD_SECRET]]B7bf8a3f8A4fCEfcC4c")]
    [InlineData("shopify-access-token", @"shpat_808a21Ced6B6F[[DD_SECRET]]6a6C0EcFCba68cADd87")]
    [InlineData("shopify-custom-access-token", @"shpca_DE49a0B2Fbc97[[DD_SECRET]]3FDD35AaB0F8d4Fb497")]
    [InlineData("shopify-private-app-access-token", @"shppa_ccBA6CadEaA8b[[DD_SECRET]]9f07aDd13c8fb66615b")]
    [InlineData("slack-bot-token", @"xoxb-57511537153-58[[DD_SECRET]]259175784BeXImt3nnS")]
    [InlineData("slack-user-token", @"xoxp-474144176626-94055636708-586108[[DD_SECRET]]3084334-vnmYyJ62yBkQj01bTUKu2OIRL03LD")]
    [InlineData("slack-app-token", @"xapp-0-DJM[[DD_SECRET]]E-962-yph7n")]
    [InlineData("slack-config-access-token", @"xoxe.xoxb-4-41X1EOYYSHTYLO8NQV5RZV3CMQGHL0HFOPWYP895XQ6ZDUTJR4R23AQI7UPXHRM19OK62K7SWA3[[DD_SECRET]]JYD81760QJPLO8N04TLXGDFZZJWRH79G2GHT4VX0K15RUC68OTC8KNWFTNJRLPTTKJTH146J5VMPIXFWNYDX4A5P")]
    [InlineData("slack-config-access-token", @"xoxe.xoxp-0-QLFP5HLHKTZHUX3DZAH75HXEWM6ZKNEXY3P80GRH6Q5CYRS9B6GOMFFIXA2TLW77OBS0I4MTT9A[[DD_SECRET]]OMUQF36XNH0KTYGKZO76JT7XW910GUX4ME05CUWZO8TZY18CBUXEVD15O73GBGR2U58P7KHE1IRK9I95AZYW7SII")]
    [InlineData("slack-config-refresh-token", @"xoxe-8-F0QXIR0E32SE1MSNNWQ5G88FMR55CXZCC3CNLUI3IDFCRU7JOE5W59KH2KL1W5CGDQC7K[[DD_SECRET]]EC2NXWD25Y1TVR71DGCS4VAIRHNA99BRA9KCCIINHP6WUHANEO9JAOPTBU6QJC26WJTWG5Z48ZLPY")]
    [InlineData("slack-legacy-bot-token", @"xoxb-6219860745-UbF[[DD_SECRET]]vVgjNa9sQxlpQhstga0")]
    [InlineData("slack-legacy-workspace-token", @"xoxa-8-ggabTQMW4rQcv[[DD_SECRET]]bA9MFpmAONEUStauyUoC")]
    [InlineData("slack-legacy-workspace-token", @"xoxr-vQZs78wKjKouC7IW[[DD_SECRET]]dadw1ssvg1nBrvwrAMFKaj")]
    [InlineData("slack-legacy-token", @"xoxo-8402[[DD_SECRET]]4-34-371-3")]
    [InlineData("slack-webhook-url", @"https://hooks.slack.com/services/YeLGw[[DD_SECRET]]7TzO961c+x5n7rK9aR2wBJ4LyQfEnXrIcg6js0")]
    [InlineData("slack-webhook-url", @"https://hooks.slack.com/workflows/gb5fx[[DD_SECRET]]cHQajG+NVWuxanbsax0mFwLawnQl+WVIqHurAEh")]
    [InlineData("square-access-token", @"sq0atp-OUp6o98[[DD_SECRET]]4Pb5g5uhhyrfxrP")]
    [InlineData("square-secret", @"sq0csp--0c6JvmiMFJDdz4edU[[DD_SECRET]]BBPGh3oR3kAQbh_Gq5B2qBevl")]
    [InlineData("stripe-access-token", @"sk_test_rn2qv[[DD_SECRET]]71gn7tpbkbvu1i")]
    [InlineData("stripe-access-token", @"sk_live_nm7c4zgkn0j[[DD_SECRET]]hi994cbvyd41sf3rmpyc")]
    [InlineData("stripe-access-token", @"pk_test_dtq5gqlv[[DD_SECRET]]qgsnnb61a7ii5jkj")]
    [InlineData("stripe-access-token", @"pk_live_fw08uup[[DD_SECRET]]ox4pj4ompz8wcd43")]
    [InlineData("telegram-bot-api-token", @"75882058:A_ITu4mHleTkS[[DD_SECRET]]Ru5MLP7G_-sA1SN5jAxc5n")]
    [InlineData("twilio-api-key", @"SKDEe788AD4BD64EB[[DD_SECRET]]e4F0963F9Fb1a69b5")]
    [InlineData("vault-service-token", @"hvs.v4d44-7cibde8q_kax-mus29jn6lkv3kyjxe16pagnc[[DD_SECRET]]e7lddehl5u8tccgvqev4zk5sy45ugh-ghqyb9k3gdj9221v")]
    [InlineData("vault-batch-token", @"hvb.vk-7wvp5c4qkkyexv2mypexfno1pmc28j3-7hevyb5-e6s7qemg-p9yqy6th5-s-cubtz[[DD_SECRET]]jprh0xwftl6tkl-82tlevinwn4cm-clro379140mclfvy0vztdmb1odab658vjfq-oetpcg94")]
    public void GivenAHardcodedSecretString_WhenAnalysed_ResultIsEspected(string rule, string secret)
    {
        var realSecret = secret.Replace("[[DD_SECRET]]", string.Empty);
        _analyzer.CheckSecret(realSecret).Should().Be(rule);
    }

    public class HardcodedSecretsFixture : IDisposable
    {
        // We use a stupidly-big timeout here to avoid flake
        internal HardcodedSecretsAnalyzer Analyzer { get; } = new(TimeSpan.FromMinutes(5));

        public void Dispose() => Analyzer.Dispose();
    }
}
