// Сборка, тесты и контейнеризация шлюза курсов валют ЦБ РФ (spec.md, D-2; design.md, ADR-11).
//
// Требования к агенту (A-10): Linux с Docker и плагином Docker Pipeline; метка 'docker'.
// .NET SDK на агенте не нужен — сборка и тесты выполняются в контейнере mcr.microsoft.com/dotnet/sdk:8.0.
// Значения registry и credentials по умолчанию условные: для публикации задайте реальные и создайте
// в Jenkins учётные данные типа «Username with password» с указанным ID.

pipeline {
    agent { label 'docker' }

    parameters {
        string(name: 'IMAGE_NAME', defaultValue: 'digital-league/currency-gateway', description: 'Имя Docker-образа')
        booleanParam(name: 'PUSH_IMAGE', defaultValue: false, description: 'Опубликовать образ в registry')
        string(name: 'REGISTRY', defaultValue: 'registry.example.com', description: 'Адрес Docker registry (условный)')
        string(name: 'REGISTRY_CREDENTIALS_ID', defaultValue: 'docker-registry-credentials', description: 'ID учётных данных Jenkins для registry (условный)')
    }

    options {
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    environment {
        IMAGE_TAG = "${env.BUILD_NUMBER}"
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        // Контейнер SDK запускается под пользователем Jenkins без домашнего каталога.
        DOTNET_CLI_HOME = '/tmp/dotnet-cli-home'
        NUGET_PACKAGES = "${env.WORKSPACE}/.nuget/packages"
    }

    stages {
        stage('Build & Test') {
            agent {
                docker {
                    image 'mcr.microsoft.com/dotnet/sdk:8.0'
                    reuseNode true
                }
            }
            stages {
                stage('Restore') {
                    steps {
                        sh 'dotnet restore DigitalLeagueTask.Tests/DigitalLeagueTask.Tests.csproj'
                    }
                }
                stage('Build') {
                    steps {
                        sh 'dotnet build DigitalLeagueTask.Tests/DigitalLeagueTask.Tests.csproj --configuration Release --no-restore'
                    }
                }
                stage('Test') {
                    steps {
                        // Включает проверку актуальности specs/openapi.yaml (ADR-10).
                        sh '''
                            dotnet test DigitalLeagueTask.Tests/DigitalLeagueTask.Tests.csproj \
                                --configuration Release --no-build \
                                --logger "junit;LogFilePath=$WORKSPACE/TestResults/junit.xml"
                        '''
                    }
                    post {
                        always {
                            junit testResults: 'TestResults/junit.xml', allowEmptyResults: false
                        }
                    }
                }
            }
        }

        stage('Docker Build') {
            steps {
                sh '''
                    docker build \
                        --label "org.opencontainers.image.revision=${GIT_COMMIT:-unknown}" \
                        --label "org.opencontainers.image.version=$IMAGE_TAG" \
                        --tag "$IMAGE_NAME:$IMAGE_TAG" \
                        .
                '''
            }
        }

        stage('Smoke Test') {
            steps {
                // Контейнер стартует и отвечает на /health (NFR-6). В aspnet-образе нет curl,
                // поэтому запрос выполняется из отдельного контейнера в сетевом пространстве сервиса.
                sh '''
                    cid=$(docker run --detach "$IMAGE_NAME:$IMAGE_TAG")
                    trap 'docker rm --force "$cid" > /dev/null' EXIT
                    for attempt in $(seq 1 30); do
                        if docker run --rm --network "container:$cid" curlimages/curl:8.10.1 \
                                --fail --silent --show-error http://localhost:8080/health; then
                            exit 0
                        fi
                        sleep 1
                    done
                    docker logs "$cid"
                    exit 1
                '''
            }
        }

        stage('Push') {
            when {
                expression { params.PUSH_IMAGE }
            }
            steps {
                withCredentials([usernamePassword(
                        credentialsId: params.REGISTRY_CREDENTIALS_ID,
                        usernameVariable: 'REGISTRY_USER',
                        passwordVariable: 'REGISTRY_PASSWORD')]) {
                    // Одинарные кавычки: секреты подставляет shell, а не Groovy, и они не попадают в лог.
                    sh '''
                        echo "$REGISTRY_PASSWORD" | docker login "$REGISTRY" --username "$REGISTRY_USER" --password-stdin
                        for tag in "$IMAGE_TAG" latest; do
                            docker tag "$IMAGE_NAME:$IMAGE_TAG" "$REGISTRY/$IMAGE_NAME:$tag"
                            docker push "$REGISTRY/$IMAGE_NAME:$tag"
                        done
                    '''
                }
            }
            post {
                always {
                    sh 'docker logout "$REGISTRY" || true'
                }
            }
        }
    }

    post {
        always {
            // Локальные образы сборки не копятся на агенте.
            sh '''
                docker image rm --force "$IMAGE_NAME:$IMAGE_TAG" > /dev/null 2>&1 || true
                docker image rm --force "$REGISTRY/$IMAGE_NAME:$IMAGE_TAG" "$REGISTRY/$IMAGE_NAME:latest" > /dev/null 2>&1 || true
            '''
        }
    }
}
