using PlanningPoker;
using Xunit;

namespace PlanningPoker.Tests
{
    public class SignatureVerifierTests
    {
        private const string Secret = "s3cr3t";
        private const string Body = "{\"question\":\"Validated?\"}";

        [Fact]
        public void Verify_accepts_a_correct_signature()
        {
            var header = "sha256=" + SignatureVerifier.ComputeHex(Body, Secret);

            Assert.True(SignatureVerifier.VerifyHmacSha256(Body, Secret, header));
        }

        [Fact]
        public void Verify_rejects_a_tampered_body()
        {
            var header = "sha256=" + SignatureVerifier.ComputeHex(Body, Secret);

            Assert.False(SignatureVerifier.VerifyHmacSha256(Body + " ", Secret, header));
        }

        [Fact]
        public void Verify_rejects_a_wrong_secret()
        {
            var header = "sha256=" + SignatureVerifier.ComputeHex(Body, "other");

            Assert.False(SignatureVerifier.VerifyHmacSha256(Body, Secret, header));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Verify_rejects_missing_header(string header)
        {
            Assert.False(SignatureVerifier.VerifyHmacSha256(Body, Secret, header));
        }

        [Fact]
        public void Verify_rejects_when_secret_unset()
        {
            var header = "sha256=" + SignatureVerifier.ComputeHex(Body, "");

            Assert.False(SignatureVerifier.VerifyHmacSha256(Body, "", header));
        }
    }
}
