
import yaml
import boto3
import time
import requests
import os
import json 

client = boto3.client(
        service_name='sqs',
        endpoint_url='https://message-queue.api.cloud.yandex.net',
        region_name='ru-central1'
    )

def failover(route_table_id,subnet_id,iam_token):
    '''
    changes route table of subnet list
    :param route_table_id: id of the route table
    :param iam_token: token for auth
    :param subnet_id:  subnet  where route table is changed
    :return:
    '''
    queue_url = os.environ.get('YMQ_URL')
    print('failing over route table %s for subnets %s' % (route_table_id,subnet_id))
    retry_rt_switch_operation("",subnet_id,iam_token)
    retry_rt_switch_operation(route_table_id,subnet_id,iam_token)

    
def retry_rt_switch_operation(route_table_id,subnet_id, iam_token,num_tries = 10):
    '''
    retries yandex cloud operation num_tries times
    '''

    for num in range(num_tries):
        r = requests.patch('https://vpc.api.cloud.yandex.net/vpc/v1/subnets/%s' % subnet_id, json={"updateMask": "routeTableId", "routeTableId": route_table_id } ,headers={'Authorization': 'Bearer %s'  % iam_token})
        operation_id = r.json()['id']
        operation_status = check_operation(operation_id,iam_token)
        if operation_status == 'ok':
            print('Operation %s was successfull' % (operation_id))
            break
        else:
            print('Operation %s was unsuccessfull retrying in 10 seconds...' % (operation_id))
            time.sleep(1)

def check_operation(operation_id,iam_token):
    '''
    waits for operation to complete
    :param operationID: id of the operation
    :param iamToken: token for auth
    :return: nothing - just stops when operation completes
    '''
    while True:
        r = requests.get('https://operation.api.cloud.yandex.net/operations/%s' % operation_id, headers={'Authorization': 'Bearer %s' % iam_token})
        operationStatus = r.json()['done']
        if 'error' in r.json():
            status = 'error'
            break 
        if operationStatus == True:
            print('Operation %s is done' % operation_id)
            status = 'ok'
            break
        time.sleep(1)
    return status

def handler(event, context):
   
    iam_token = context.token['access_token']

    for msg in event['messages']:
        
        object_data = json.loads(msg['details']['message']['body'])
        print(object_data)
        subnet_id = object_data['subnet_id']
        route_table_id = object_data['route_table_id']
        failover(route_table_id,subnet_id,iam_token)
