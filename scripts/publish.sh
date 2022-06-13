set -e

AWS_ACCOUNT_ID=$(aws sts get-caller-identity --profile kiriworks-sso --query "Account" --output text)
ECR_URI="${AWS_ACCOUNT_ID}.dkr.ecr.us-east-2.amazonaws.com"

rm -rf out
dotnet publish -c Release -o out
docker build -t kw-pdf-services:latest .
docker tag kw-pdf-services:latest $ECR_URI/usps-address-validator:latest
docker push $ECR_URI/usps-address-validator:latest

